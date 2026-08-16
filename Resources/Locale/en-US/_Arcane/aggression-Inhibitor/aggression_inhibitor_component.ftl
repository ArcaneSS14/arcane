stabikor-activate-verb = Activate
stabikor-activated-success = The { $item } snaps shut!
stabikor-not-equipped = Object not detected.
stabikor-moment-shutdown = The { $item } unfastens and falls to the ground!
stabikor-disarm-shock-popup = You are severely shocked for attempting aggression! Combat mode disabled.

stabikor-examine-status-main = The device mode indicator lights up: [color=yellow]{$mode}[/color].

stabikor-examine-duration-info = Operation time is set to{ $hours ->
    [0] { "" }
    [one] {" "}[color=cyan]{$hours}[/color] hour
    *[other] {" "}[color=cyan]{$hours}[/color] hours
}{ $hours ->
    [0] { "" }
    *[other] { $minutes ->
        [0] { "" }
        *[other] {" and"}
    }
}{ $minutes ->
    [0] { "" }
    [one] {" "}[color=cyan]{$minutes}[/color] minute
    *[other] {" "}[color=cyan]{$minutes}[/color] minutes
}.

stabikor-examine-timer-remaining =
    Time remaining until shutdown:{ $hours ->
        [0] { "" }
       *[other] {" "}[color=orange]{$hours}[/color] { $hours ->
            [one] hour
           *[other] hours
        }
    }{ $hours ->
        [0] { "" }
       *[other] { $minutes ->
            [0] { "" }
           *[other] {" and"}
        }
    }{ $minutes ->
        [0] { "" }
       *[other] {" "}[color=orange]{$minutes}[/color] { $minutes ->
            [one] minute
           *[other] minutes
        }
    }{ $hours ->
        [0] { $minutes ->
            [0] { "" }
           *[other] { $seconds ->
                [0] { "" }
               *[other] {" and"}
            }
        }
       *[other] { $seconds ->
            [0] { "" }
           *[other] {" and"}
        }
    }{ $seconds ->
        [0] { "" }
       *[other] {" "}[color=orange]{$seconds}[/color] { $seconds ->
            [one] second
           *[other] seconds
        }
    }.

stabikor-examine-locked = locked
stabikor-examine-unlocked = standby

stabikor-verb-set-duration = Set Timer
stabikor-verb-lock = Activate Lock
stabikor-verb-unlock = Release Lock

stabikor-dialog-title = Timer Configuration
stabikor-dialog-field =
    Enter the device operation time
    (in minutes, from 1 to 900):
stabikor-dialog-invalid-range = Invalid time range! Enter a number from 1 to 900.
stabikor-duration-set-success = Operation time successfully set to {$time} min.
stabikor-duration-set-cancel-fallback = Input canceled, set to default time: {$time} min.

stabikor-unequip-blocked-active = The device cannot be removed until the timer expires!
