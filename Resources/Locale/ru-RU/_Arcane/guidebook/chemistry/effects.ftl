reagent-effect-guidebook-oxygenate =
    { $chance ->
        [1] Улучшает оксигенацию на { NATURALFIXED($factor, 1) } и замедляет дальнейшее получение урона от удушья
       *[other] Может улучшить оксигенацию на { NATURALFIXED($factor, 1) } и замедлить дальнейшее получение урона от удушья
    }

reagent-effect-guidebook-convermol =
    { $chance ->
        [1] Лечит гипоксию ({ $rate } урона/тик), создавая токсины в пропорции 1:{ $ratio } от вылеченного урона. Порог передозировки: { $od } ед.
       *[other] С вероятностью { NATURALPERCENT($chance, 1) } лечит удушье с токсическим побочным эффектом.
    }
