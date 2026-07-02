reagent-name-convermol = конвермол
reagent-desc-convermol = Мощное средство от гипоксии с токсическим побочным эффектом. При передозировке снимается ограничение на лечение, что усиливает побочную токсичность.
reagent-physical-desc-convermol = кисловатое
reagent-effect-guidebook-convermol =
    { $chance ->
        [1] Лечит гипоксию ({ $rate } урона/ед. реагента), создавая токсины в пропорции 1:{ $ratio } от вылеченного урона. Порог передозировки: { $od } ед.
       *[other] С вероятностью { NATURALPERCENT($chance, 1) } лечит удушье с токсическим побочным эффектом.
    }

reagent-name-salbutamol = сальбутамол
reagent-desc-salbutamol = Замедляет дальнейшее удушье и стабилизирует дыхание пациента. Хорошо подходит для экстренной стабилизации.
reagent-physical-desc-salbutamol = прозрачное
