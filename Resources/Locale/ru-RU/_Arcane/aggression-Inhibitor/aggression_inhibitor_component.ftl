stabikor-activate-verb = Активировать
stabikor-activated-success = { $item } защелкивается!
stabikor-not-equipped = Объект не обнаружен.
stabikor-moment-shutdown = { $item } расстегивается и падает на землю!
stabikor-disarm-shock-popup = Вас сильно бьет током за попытку агрессии! Боевой режим отключен.

stabikor-examine-status-main = На устройстве горит индикатор режима: [color=yellow]{$mode}[/color].

stabikor-examine-duration-info = Время работы настроено на{ $hours ->
    [0] { "" }
    [one] {" "}[color=cyan]{$hours}[/color] час
    [few] {" "}[color=cyan]{$hours}[/color] часа
    *[other] {" "}[color=cyan]{$hours}[/color] часов
}{ $hours ->
    [0] { "" }
    *[other] { $minutes ->
        [0] { "" }
        *[other] {" и"}
    }
}{ $minutes ->
    [0] { "" }
    [one] {" "}[color=cyan]{$minutes}[/color] минуту
    [few] {" "}[color=cyan]{$minutes}[/color] минуты
    *[other] {" "}[color=cyan]{$minutes}[/color] минут
}.

stabikor-examine-timer-remaining =
    До отключения осталось:{ $hours ->
        [0] { "" }
       *[other] {" "}[color=orange]{$hours}[/color] { $hours ->
            [one] час
            [few] часа
           *[other] часов
        }
    }{ $hours ->
        [0] { "" }
       *[other] { $minutes ->
            [0] { "" }
           *[other] {" и"}
        }
    }{ $minutes ->
        [0] { "" }
       *[other] {" "}[color=orange]{$minutes}[/color] { $minutes ->
            [one] минута
            [few] минуты
           *[other] минут
        }
    }{ $hours ->
        [0] { $minutes ->
            [0] { "" }
           *[other] { $seconds ->
                [0] { "" }
               *[other] {" и"}
            }
        }
       *[other] { $seconds ->
            [0] { "" }
           *[other] {" и"}
        }
    }{ $seconds ->
        [0] { "" }
       *[other] {" "}[color=orange]{$seconds}[/color] { $seconds ->
            [one] секунда
            [few] секунды
           *[other] секунд
        }
    }.

stabikor-examine-locked = заблокирован
stabikor-examine-unlocked = ожидание

stabikor-verb-set-duration = Настроить таймер
stabikor-verb-lock = Активировать блокировку
stabikor-verb-unlock = Снять блокировку

stabikor-dialog-title = Конфигурация таймера
stabikor-dialog-field =
    Введите время работы устройства
    (в минутах, от 1 до 900):
stabikor-dialog-invalid-range = Неверный диапазон времени! Введите число от 1 до 900.
stabikor-duration-set-success = Время работы успешно настроено на {$time} мин.
stabikor-duration-set-cancel-fallback = Ввод отменён, установлено время по умолчанию: {$time} мин.

stabikor-unequip-blocked-active = Устройство невозможно снять до окончания таймера!
