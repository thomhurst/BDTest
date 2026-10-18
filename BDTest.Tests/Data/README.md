# Historical report fixture

`JsonData-v2.0.60.json` is the original, unmodified report snapshot added in commit
`038ba08` ("Json output snapshot", 2021-09-24). Its filename records v2.0.60;
the report's embedded `Version` is `1.0.0.0`. Do not regenerate it with the
current serializer.

The snapshot contains 13 scenarios (3 passed, 6 failed, 4 inconclusive), step
exceptions, custom test information, and startup/teardown output. Step output
is empty or null. It predates tags, retry attempt history, scenario outlines,
timeouts, and report custom properties; those fields are deliberately absent.

`JsonResultsTests` checks stable report fields before and after current core
serialization. `AzureStorageDataStoreTests` loads the same file through the
production storage reader and checks the current storage serialization round
trip. `ReportUploadTests` exercises the upload and rendered report pages with
the historical payload. Both test projects copy this one fixture to their
output directories so the tests do not depend on the working directory.
