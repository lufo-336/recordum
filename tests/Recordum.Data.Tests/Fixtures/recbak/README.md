Reference `.recbak` files (`linux.recbak`, `windows.recbak`), passphrase `cross-platform-reference` (fake data). Same seed as `RecbakReferenceSeed`.
Regenerate: `RECORDUM_GENERATE_RECBAK=<folder> dotnet test tests/Recordum.Data.Tests --filter Category=FixtureGenerator` writes `<os>.recbak`; Windows one comes from the CI artifact `recbak-fixture-windows`.
