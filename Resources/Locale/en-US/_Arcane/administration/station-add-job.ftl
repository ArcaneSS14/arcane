cmd-stationaddjob-desc = Changes job slots on a station.
cmd-stationaddjob-help = Usage: {$command} <station> <job> <set|add|remove> <amount>
    set - sets the slot count. -1 makes the job unlimited (set only).
    add - adds slots. Creates the job on the station if it wasn't there.
    remove - removes slots, down to 0.
cmd-stationaddjob-hint-station = <station>
cmd-stationaddjob-hint-job = <job>
cmd-stationaddjob-hint-mode = <set|add|remove>
cmd-stationaddjob-hint-amount = <amount>
cmd-stationaddjob-hint-unlimited = unlimited
cmd-stationaddjob-mode-set = set slot count
cmd-stationaddjob-mode-add = add slots
cmd-stationaddjob-mode-remove = remove slots
cmd-stationaddjob-invalid-station = No station found with id {$station}.
cmd-stationaddjob-invalid-job = No job found with id {$job}.
cmd-stationaddjob-invalid-mode = Unknown mode {$mode}. Use set, add or remove.
cmd-stationaddjob-invalid-amount = Amount must be 0 or more. -1 is only allowed with set.
cmd-stationaddjob-unlimited-adjust = {$job} on {$station} is unlimited. Use set to change it.
cmd-stationaddjob-no-slot = {$station} has no {$job} job.
cmd-stationaddjob-result = {$job} on {$station}: {$total} slots.
cmd-stationaddjob-result-unlimited = {$job} on {$station}: unlimited.
