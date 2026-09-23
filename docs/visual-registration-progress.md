# Visual preset registration progress

The companion uses `visual.presets.registration.start` with the normal import
parameters plus a UUID `operationId`. It receives `operation_id` immediately, then
polls `visual.presets.registration.get` with the same ID every 400 ms after the
previous response. No overlapping status requests or repeated import requests are
needed. The existing synchronous inspect/import methods remain available to older
clients, but the registration dialog does not use them.

This must run off the IPC listener: AdofaiIpc handles ordinary requests serially.
`VisualRegistrationJobs` runs the job on a worker, leaving the listener available
for status requests. It retains only small status/result objects, admits at most
two active jobs, and removes results older than five minutes when starting another
job. Repeated starts with an existing ID do not create a second preset. A feature
shutdown cancels work; asset callbacks and the server request observe cancellation.

The worker reports `reading_settings`, `processing_assets`, `validating` and
`uploading`. File names are basenames, and `completed_assets` counts assets actually
added to the bundle. The UI uses phase states, not fabricated overall percentages.
The server phase covers both transfer and server response; bytes uploaded are not
currently measured. The browser also displays attachment preparation before the
job starts.

Final states are `completed` with preset metadata, `needs_assets` with attachment
requirements, or `failed` with a domain error code. The inspected bundle is uploaded
directly once complete. Account identity is captured before background work and
checked again before contacting the server.

Connection failures during polling retry status reads up to three times. If status
is lost or polling exceeds ten minutes, the UI asks the user to refresh the preset
list before retrying, because the server may have completed registration. This
does not cancel or automatically repeat registration.
