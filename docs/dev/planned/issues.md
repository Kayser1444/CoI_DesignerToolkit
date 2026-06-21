# Issues

* **Ship Pollution Tracking**: Ships aren't working/recording pollution because fuel is only consumed when they are moving on the world map, not during their local dock operations or idle cycles. We need a way to track their fuel consumption or nominal pollution based on their world map voyages.


## Resolved

* **Insta-demolish storage contents**: Fixed in BDT's Instant Build Mode by automatically clearing any storable product buffers of `StorageBase` entities when deconstruction is requested (staged or active).
