(function () {
  const inputEl  = document.getElementById('input');
  const outputEl = document.getElementById('output-area');
  const inBadge  = document.getElementById('input-badge');
  const outBadge = document.getElementById('output-badge');

  let debounce = null;

  inputEl.addEventListener('input', () => {
    clearTimeout(debounce);
    debounce = setTimeout(decode, 200);
  });

  // ── Tab switching ────────────────────────────────────────────────────
  let activeTab = 'parsed';

  function switchTab(name) {
    activeTab = name;
    outputEl.querySelectorAll('.tab').forEach(t => t.classList.toggle('active', t.dataset.tab === name));
    outputEl.querySelectorAll('.tab-content').forEach(c => c.classList.toggle('active', c.dataset.tab === name));
  }

  // ── Main decode ──────────────────────────────────────────────────────
  function decode() {
    const raw = inputEl.value.trim();

    if (!raw) {
      inBadge.textContent = '';
      inBadge.className = 'badge';
      outBadge.textContent = '';
      outBadge.className = 'badge';
      outputEl.innerHTML = '<div class="empty">Waiting for input&#8230;</div>';
      return;
    }

    try {
      let type = 'Unknown';
      let b64 = raw;

      const m = raw.match(/^([BF])(\d+):(.+)$/s);
      if (m) {
        type = m[1] === 'B' ? 'Blueprint' : 'Folder';
        b64 = m[3].replace(/\s/g, '');
      }

      inBadge.textContent = type;
      inBadge.className = 'badge ok';

      const compressed = base64ToBytes(b64);
      const data = pako.inflate(compressed);

      outBadge.textContent = 'OK';
      outBadge.className = 'badge ok';

      render(type, compressed, data);

    } catch (err) {
      inBadge.textContent = 'Error';
      inBadge.className = 'badge err';
      outBadge.textContent = 'Error';
      outBadge.className = 'badge err';
      outputEl.innerHTML = `<div class="error-box">${escHtml(String(err))}</div>`;
    }
  }

  // ── BlobReader ───────────────────────────────────────────────────────
  // Mirrors Mafi.Core BlobWriter/BlobReader:
  //  - integers: variable-length LEB128; signed uses zig-zag encoding
  //  - strings:  refIndex (UInt) — if refIndex == table.length it is a new
  //              string (7-bit length + UTF-8 bytes follow); else back-ref
  //  - booleans: 1 byte (0x00 / 0x01)
  //  - byte arrays: UInt length + raw bytes

  class BlobReader {
    constructor(bytes) {
      this.bytes = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
      this.pos = 0;
      this.stringTable = [null]; // null is pre-inserted at index 0 by BlobWriter.resetMap()
    }

    get remaining() { return this.bytes.length - this.pos; }

    readByte() {
      if (this.pos >= this.bytes.length)
        throw new Error(`Unexpected end of data at 0x${this.pos.toString(16)}`);
      return this.bytes[this.pos++];
    }

    // Unsigned variable-length integer (7 bits per byte, high bit = more follows)
    readUInt() {
      let result = 0, shift = 0;
      for (;;) {
        const b = this.readByte();
        result |= (b & 0x7f) << shift;
        if ((b & 0x80) === 0) break;
        shift += 7;
      }
      return result >>> 0;
    }

    // Signed integer — zig-zag decoded from UInt
    readInt() {
      const n = this.readUInt();
      return (n >>> 1) ^ -(n & 1);
    }

    readIntNotNegative() { return this.readUInt(); }

    readBool() { return this.readByte() !== 0; }

    // String with interning table (matches BlobWriter.WriteString)
    readString() {
      const ref = this.readIntNotNegative();
      if (ref === this.stringTable.length) {
        // New string: 7-bit encoded byte length, then UTF-8 bytes
        const len = this.readUInt();
        const slice = this.bytes.subarray(this.pos, this.pos + len);
        this.pos += len;
        const str = new TextDecoder('utf-8').decode(slice);
        this.stringTable.push(str);
        return str;
      }
      if (ref < this.stringTable.length) return this.stringTable[ref];
      throw new Error(
        `String ref ${ref} out of range (table size ${this.stringTable.length}) at 0x${this.pos.toString(16)}`
      );
    }

    // Raw byte array: UInt length + bytes
    readByteArray() {
      const len = this.readIntNotNegative();
      const slice = this.bytes.slice(this.pos, this.pos + len);
      this.pos += len;
      return slice;
    }
  }

  // ── Blueprint parser ─────────────────────────────────────────────────
  // Top-level blob layout (after base64+gzip):
  //   gameVersion : String
  //   saveVersion : IntNotNegative
  //   name        : String
  //   description : String
  //   entityCount : IntNotNegative
  //   entities[]  : EntityConfigData  (see readEntity)
  //   <surfaces, decals, overlapDeltas — complex spatial compression, not parsed>
  //
  // EntityConfigData holds 5 typed dictionaries, in order:
  //   integers dict    : count + (key:String, value:Int)[]
  //   booleans dict    : count + (key:String, value:Bool)[]
  //   strings dict     : count + (key:String, value:String)[]
  //   stringArrays dict: count + (key:String, arrLen:Int, elems:String[])[]
  //   byteArrays dict  : count + (key:String, bytes:ByteArray)[]
  //
  // The "Transform" byte array encodes TileTransform as:
  //   x:Int, y:Int, z:Int, rotation:Int (0-3), reflected:Bool

  const ROTATIONS = ['0\xb0', '90\xb0', '180\xb0', '270\xb0'];

  function parseBlueprint(data) {
    const r = new BlobReader(data);
    const bp = { entities: [] };
    try {
      // BlueprintsLibrary.convertToString writes the library version before the blueprint
      bp.libraryVersion = r.readIntNotNegative();
      bp.gameVersion = r.readString();
      bp.saveVersion = r.readIntNotNegative();
      bp.name        = r.readString();
      bp.description = r.readString();

      const count = r.readIntNotNegative();
      for (let i = 0; i < count; i++) {
        bp.entities.push(readEntity(r));
      }

      bp.parsedUpTo   = r.pos;
      bp.remaining    = r.remaining;
      bp.parseSuccess = true;
    } catch (err) {
      bp.parseError = err.message;
      bp.parsedUpTo = r.pos;
    }
    return bp;
  }

  function readEntity(r) {
    const e = { integers: {}, booleans: {}, strings: {}, stringArrays: {}, byteArrays: {} };

    let n = r.readIntNotNegative();
    for (let i = 0; i < n; i++) { const k = r.readString(); e.integers[k] = r.readInt(); }

    n = r.readIntNotNegative();
    for (let i = 0; i < n; i++) { const k = r.readString(); e.booleans[k] = r.readBool(); }

    n = r.readIntNotNegative();
    for (let i = 0; i < n; i++) { const k = r.readString(); e.strings[k] = r.readString(); }

    n = r.readIntNotNegative();
    for (let i = 0; i < n; i++) {
      const k = r.readString();
      const len = r.readIntNotNegative();
      const arr = [];
      for (let j = 0; j < len; j++) arr.push(r.readString());
      e.stringArrays[k] = arr;
    }

    n = r.readIntNotNegative();
    for (let i = 0; i < n; i++) {
      const k = r.readString();
      e.byteArrays[k] = r.readByteArray();
    }

    // Decode TileTransform from its own byte array (independent BlobReader, no string table)
    const tf = e.byteArrays['Transform'];
    if (tf) {
      try {
        const tr = new BlobReader(tf);
        e.transform = {
          x: tr.readInt(), y: tr.readInt(), z: tr.readInt(),
          rotation: tr.readInt(),
          reflected: tr.readBool()
        };
      } catch (_) { /* leave e.transform undefined */ }
    }

    // Decode PrioritizedPorts: count(UInt) + count×bool(1 byte each)
    // Used by Zipper (pipe/conveyor balancer) entities to mark which ports are high-priority
    const ppRaw = e.byteArrays['PrioritizedPorts'];
    if (ppRaw) {
      try {
        const tr = new BlobReader(ppRaw);
        const count = tr.readUInt();
        e.prioritizedPorts = [];
        for (let j = 0; j < count; j++) e.prioritizedPorts.push(tr.readBool());
      } catch (_) { /* leave e.prioritizedPorts undefined */ }
    }

    // Decode Trajectory (transport/conveyor entities use this instead of Transform)
    // Format: startDir(Int×3) endDir(Int×3) pivotCount(UInt) pivots[](Int×3 each)
    const traj = e.byteArrays['Trajectory'];
    if (traj && !e.transform) {
      try {
        const tr = new BlobReader(traj);
        const startDir = { x: tr.readInt(), y: tr.readInt(), z: tr.readInt() };
        const endDir   = { x: tr.readInt(), y: tr.readInt(), z: tr.readInt() };
        const pivotCount = tr.readUInt();
        const pivots = [];
        for (let j = 0; j < pivotCount; j++) {
          pivots.push({ x: tr.readInt(), y: tr.readInt(), z: tr.readInt() });
        }
        e.trajectory = { startDir, endDir, pivots };
      } catch (_) { /* leave e.trajectory undefined */ }
    }

    return e;
  }

  // Convert a RelTile3i direction vector to a short compass label
  function dirLabel(d) {
    if (d.x === 0  && d.y === 1  && d.z === 0)  return 'N';
    if (d.x === 0  && d.y === -1 && d.z === 0)  return 'S';
    if (d.x === 1  && d.y === 0  && d.z === 0)  return 'E';
    if (d.x === -1 && d.y === 0  && d.z === 0)  return 'W';
    if (d.x === 0  && d.y === 0  && d.z === 1)  return 'U';
    if (d.x === 0  && d.y === 0  && d.z === -1) return 'D';
    if (d.x === 0  && d.y === 0  && d.z === 0)  return '\u2022'; // null/unset direction
    return `(${d.x},${d.y},${d.z})`;
  }

  // ── Render: Parsed tab ───────────────────────────────────────────────
  function renderParsed(data) {
    const bp = parseBlueprint(data);
    const entities = bp.entities || [];

    const hdr = `
      <div class="info-bar">
        <div class="info-chip"><span class="label">Name</span><span class="value">${escHtml(bp.name ?? '\u2014')}</span></div>
        <div class="info-chip"><span class="label">Game version</span><span class="value">${escHtml(bp.gameVersion ?? '\u2014')}</span></div>
        <div class="info-chip"><span class="label">Save version</span><span class="value">${bp.saveVersion ?? '\u2014'}</span></div>
        <div class="info-chip"><span class="label">Library version</span><span class="value">${bp.libraryVersion ?? '\u2014'}</span></div>
        <div class="info-chip"><span class="label">Entities</span><span class="value">${entities.length}</span></div>
        ${bp.description ? `<div class="info-chip"><span class="label">Desc</span><span class="value">${escHtml(bp.description)}</span></div>` : ''}
      </div>`;

    if (bp.parseError && entities.length === 0)
      return hdr + `<div class="error-box">Parse failed at 0x${bp.parsedUpTo.toString(16)}: ${escHtml(bp.parseError)}</div>`;

    const anyReflected    = entities.some(e => e.transform?.reflected);
    const anyTitle        = entities.some(e => e.strings['CustomTitle']);
    const anyTrajectory   = entities.some(e => e.trajectory);
    const anyPrioPorts    = entities.some(e => e.prioritizedPorts);

    const rows = entities.map((e, i) => {
      const proto    = e.strings['Prototype'] ?? '?';
      const mod      = e.strings['ProtoModName'];
      const tf       = e.transform;
      const traj     = e.trajectory;
      const startPos = tf ?? traj?.pivots?.[0] ?? null;
      const endPos   = traj?.pivots?.[traj.pivots.length - 1] ?? null;
      const ptCount  = traj?.pivots?.length ?? 0;
      const rot      = tf
        ? (ROTATIONS[tf.rotation] ?? tf.rotation)
        : traj
          ? `${dirLabel(traj.startDir)}\u2192${dirLabel(traj.endDir)}`
          : '?';
      // Show pivot count annotation when path has more than 2 pivot points (complex curve)
      const ptNote   = traj && ptCount > 2 ? `<span class="td-mod"> (${ptCount}\u00a0pts)</span>` : '';
      // Build tooltip showing all intermediate pivot points (i.e. the bend/kink positions)
      const midPivots = traj ? traj.pivots.slice(1, -1) : [];
      const rowTitle  = midPivots.length
        ? `title="${midPivots.map((p, j) => `pt${j + 2}: (${p.x}, ${p.y}, ${p.z})`).join('  ')}"`
        : '';
      // Prioritized ports: render as compact dot-pattern, e.g. ···★··· (★ = high priority)
      // ForceEvenInputs / ForceEvenOutputs shown as small badges on the proto cell
      const pp       = e.prioritizedPorts;
      const ppCell   = pp
        ? pp.map((v, j) => v
            ? `<span class="pp-on" title="Port ${j} prioritized">\u2605</span>`
            : `<span class="pp-off">\u00b7</span>`).join('')
        : '\u2014';
      const evenIn   = e.booleans['ForceEvenInputs'];
      const evenOut  = e.booleans['ForceEvenOutputs'];
      const evenBadge = (evenIn  ? '<span class="td-mod" title="Enforce strictly even inputs"> EvenIn</span>' : '')
                      + (evenOut ? '<span class="td-mod" title="Enforce strictly even outputs"> EvenOut</span>' : '');
      return `<tr ${rowTitle} style="${midPivots.length ? 'cursor:help' : ''}">
        <td class="td-num">${i + 1}</td>
        <td class="td-proto">${escHtml(proto)}${mod ? `<span class="td-mod"> [${escHtml(mod)}]</span>` : ''}${evenBadge}</td>
        <td class="td-num">${startPos ? startPos.x : '?'}</td>
        <td class="td-num">${startPos ? startPos.y : '?'}</td>
        <td class="td-num">${startPos ? startPos.z : '?'}</td>
        ${anyTrajectory ? `<td class="td-num">${endPos ? endPos.x : '\u2014'}</td><td class="td-num">${endPos ? endPos.y : '\u2014'}</td><td class="td-num">${endPos ? endPos.z : '\u2014'}</td>` : ''}
        <td class="td-num">${rot}${ptNote}</td>
        ${anyPrioPorts ? `<td class="td-ports">${ppCell}</td>` : ''}
        ${anyReflected ? `<td class="td-num">${tf?.reflected ? '\u21d4' : ''}</td>` : ''}
        ${anyTitle ? `<td class="td-str">${escHtml(e.strings['CustomTitle'] ?? '')}</td>` : ''}
      </tr>`;
    }).join('');

    const table = entities.length ? `
      <div style="overflow-x:auto">
        <table class="entity-table">
          <thead><tr>
            <th>#</th><th>Proto ID</th>
            <th>X</th><th>Y</th><th>Z</th>
            ${anyTrajectory ? '<th>End X</th><th>End Y</th><th>End Z</th>' : ''}
            <th>Rot / Dir</th>
            ${anyPrioPorts  ? '<th>Ports</th>' : ''}
            ${anyReflected  ? '<th>\u21d4</th>' : ''}
            ${anyTitle      ? '<th>Title</th>' : ''}
          </tr></thead>
          <tbody>${rows}</tbody>
        </table>
      </div>` : '<div class="empty">No entities.</div>';

    const note = bp.parseError
      ? `<div class="error-box" style="margin-top:10px">Partial parse \u2014 stopped at 0x${bp.parsedUpTo.toString(16)}: ${escHtml(bp.parseError)}</div>`
      : bp.remaining > 0
        ? `<div class="parse-note">${fmtBytes(bp.remaining)} remaining after entities (surfaces, decals, overlap data \u2014 not shown)</div>`
        : '';

    return hdr + table + note;
  }

  // ── Render output ────────────────────────────────────────────────────
  function render(type, compressed, data) {
    const ratio   = ((1 - compressed.length / data.length) * 100).toFixed(1);
    const strings = extractStrings(data, 4);

    outputEl.innerHTML = `
      <div class="info-bar">
        <div class="info-chip"><span class="label">Type</span><span class="value">${escHtml(type)}</span></div>
        <div class="info-chip"><span class="label">Compressed</span><span class="value">${fmtBytes(compressed.length)}</span></div>
        <div class="info-chip"><span class="label">Decompressed</span><span class="value">${fmtBytes(data.length)}</span></div>
        <div class="info-chip"><span class="label">Ratio</span><span class="value">${ratio}%</span></div>
      </div>
      <div class="tabs">
        <div class="tab${activeTab === 'parsed'  ? ' active' : ''}" data-tab="parsed">Parsed</div>
        <div class="tab${activeTab === 'strings' ? ' active' : ''}" data-tab="strings">Strings (${strings.length})</div>
        <div class="tab${activeTab === 'hex'     ? ' active' : ''}" data-tab="hex">Hex dump</div>
      </div>
      <div class="tab-content${activeTab === 'parsed'  ? ' active' : ''}" data-tab="parsed">
        ${type === 'Blueprint' ? renderParsed(data) : '<div class="empty">Folder parsing not yet supported.</div>'}
      </div>
      <div class="tab-content${activeTab === 'strings' ? ' active' : ''}" data-tab="strings">
        ${renderStrings(strings)}
      </div>
      <div class="tab-content${activeTab === 'hex' ? ' active' : ''}" data-tab="hex">
        ${renderHex(data)}
      </div>
    `;

    outputEl.querySelectorAll('.tab').forEach(t => {
      t.addEventListener('click', () => switchTab(t.dataset.tab));
    });
  }

  // ── Strings extractor ────────────────────────────────────────────────
  function extractStrings(bytes, minLen) {
    const results = [];
    let start = -1;

    for (let i = 0; i <= bytes.length; i++) {
      const c = i < bytes.length ? bytes[i] : 0;
      const printable = c >= 0x20 && c <= 0x7e;

      if (printable) {
        if (start === -1) start = i;
      } else {
        if (start !== -1 && (i - start) >= minLen) {
          let s = '';
          for (let j = start; j < i; j++) s += String.fromCharCode(bytes[j]);
          results.push({ offset: start, value: s });
        }
        start = -1;
      }
    }

    return results;
  }

  function renderStrings(strings) {
    if (!strings.length) return '<div class="empty">No printable strings found.</div>';

    const rows = strings.map(s => {
      const cls = s.value.length >= 12 ? 'long' : '';
      return `<div class="str-item">
        <span class="str-offset">0x${s.offset.toString(16).padStart(6, '0')}</span>
        <span class="str-len">${s.value.length}</span>
        <span class="str-value ${cls}">${escHtml(s.value)}</span>
      </div>`;
    }).join('');

    return `<div class="strings-list">${rows}</div>`;
  }

  // ── Hex dump ─────────────────────────────────────────────────────────
  const HEX_LIMIT = 8192;

  function renderHex(bytes) {
    const limit = Math.min(bytes.length, HEX_LIMIT);
    const lines = [];

    for (let i = 0; i < limit; i += 16) {
      const chunk = bytes.slice(i, i + 16);
      const offset = i.toString(16).padStart(8, '0');

      let hexPart = '';
      let asciiPart = '';

      for (let j = 0; j < 16; j++) {
        if (j === 8) hexPart += ' ';
        if (j < chunk.length) {
          hexPart += chunk[j].toString(16).padStart(2, '0') + ' ';
          const c = chunk[j];
          asciiPart += (c >= 0x20 && c <= 0x7e) ? String.fromCharCode(c) : '.';
        } else {
          hexPart += '   ';
          asciiPart += ' ';
        }
      }

      lines.push(
        `<span class="hex-offset">${offset}</span>  ${escHtml(hexPart)} <span class="hex-ascii">|${escHtml(asciiPart)}|</span>`
      );
    }

    const truncNote = bytes.length > HEX_LIMIT
      ? `\n<span style="color:var(--muted)">[&#8230; ${fmtBytes(bytes.length - HEX_LIMIT)} more not shown]</span>`
      : '';

    return `<div class="hex-dump">${lines.join('\n')}${truncNote}</div>`;
  }

  // ── Helpers ──────────────────────────────────────────────────────────
  function base64ToBytes(b64) {
    const binary = atob(b64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
    return bytes;
  }

  function fmtBytes(n) {
    if (n < 1024) return n + ' B';
    if (n < 1024 * 1024) return (n / 1024).toFixed(1) + ' KB';
    return (n / (1024 * 1024)).toFixed(2) + ' MB';
  }

  function escHtml(str) {
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  }
})();