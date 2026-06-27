# Blueprint Recycle Bin

BDT can protect blueprint book edits by copying deleted or updated blueprints/folders into a configurable recycle bin folder before the original action completes.

## Enable It

Open BDT's mod settings and enable **Use recycle bin** under **RECYCLE BIN**.

The default folder name is `Recycle Bin`. You can change it in settings. Folder names must be non-empty, non-whitespace, and no longer than 60 characters.

## What Gets Saved

When you delete or update a blueprint or folder outside the recycle bin, BDT copies it into the root-level recycle bin folder first.

The original parent folder path is recreated inside the recycle bin. For example, deleting `/Temp/ABC/MyBlueprint` stores a copy under `/Recycle Bin/Temp/ABC/`.

If a copied item would collide with an existing name, BDT appends a numeric suffix such as `_0`, `_1`, or `_2`.

## Deletion Behavior

Deletions outside the recycle bin skip the normal confirmation popup when the feature is enabled, because a backup copy is created automatically.

Deletions inside the recycle bin remain permanent and still use the normal confirmation popup. Deleting the recycle bin folder itself also remains permanent.

## Save Safety

The recycle bin stores normal blueprint book entries. It does not make exported blueprints depend on BDT.
