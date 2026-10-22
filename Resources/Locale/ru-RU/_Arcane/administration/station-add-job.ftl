cmd-stationaddjob-desc = Изменяет количество слотов должности на станции.
cmd-stationaddjob-help = Использование: {$command} <станция> <должность> <set|add|remove> <количество>
    set - задать количество слотов. -1 делает должность бесконечной (только для set).
    add - добавить слоты. Если должности на станции не было, она будет создана.
    remove - убрать слоты, но не меньше 0.
cmd-stationaddjob-hint-station = <станция>
cmd-stationaddjob-hint-job = <должность>
cmd-stationaddjob-hint-mode = <set|add|remove>
cmd-stationaddjob-hint-amount = <количество>
cmd-stationaddjob-hint-unlimited = бесконечно
cmd-stationaddjob-mode-set = задать количество
cmd-stationaddjob-mode-add = добавить слоты
cmd-stationaddjob-mode-remove = убрать слоты
cmd-stationaddjob-invalid-station = Станция с id {$station} не найдена.
cmd-stationaddjob-invalid-job = Должность с id {$job} не найдена.
cmd-stationaddjob-invalid-mode = Неизвестный режим {$mode}. Доступны set, add и remove.
cmd-stationaddjob-invalid-amount = Количество должно быть 0 или больше. -1 можно только с set.
cmd-stationaddjob-unlimited-adjust = Должность {$job} на {$station} бесконечная. Чтобы изменить, используйте set.
cmd-stationaddjob-no-slot = На {$station} нет должности {$job}.
cmd-stationaddjob-result = {$job} на {$station}: слотов {$total}.
cmd-stationaddjob-result-unlimited = {$job} на {$station}: бесконечно.
