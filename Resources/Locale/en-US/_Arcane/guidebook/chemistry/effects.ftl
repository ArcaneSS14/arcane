reagent-effect-guidebook-oxygenate =
    { $chance ->
        [1] Improves oxygenation by { NATURALFIXED($factor, 1) } and slows further suffocation damage.
       *[other] With { NATURALPERCENT($chance, 1) } chance, improves oxygenation by { NATURALFIXED($factor, 1) } and slows further suffocation damage.
    }

reagent-effect-guidebook-convermol =
    { $chance ->
        [1] Heals asphyxiation ({ $rate } u/u reagent), producing toxins at a 1:{ $ratio } ratio. Overdose threshold: { $od } u.
       *[other] With { NATURALPERCENT($chance, 1) } chance, heals asphyxiation with toxic side effects.
    }
