# Прототипы сущностей (Canvas, Paintbrush, PaintCan)

ent-BasePaintCanvas = холст
    .desc = Чистый холст. Используйте на нём мелок, краску или палитру, чтобы начать рисовать.

ent-PaintCanvas = холст
    .desc = Чистый квадратный холст. Используйте на нём мелок, краску или палитру, чтобы начать рисовать.

ent-PaintCanvasLandscape = горизонтальный холст
    .desc = Широкий чистый холст, хорошо подходит для пейзажей.

ent-PaintCanvasSmall = маленький холст
    .desc = Крошечный чистый холст, идеален для каракулей и портретов.

ent-Paintbrush = палитра художника
    .desc = Палитра и кисть, чтобы смешивать любой цвет, какой захотите.

ent-PaintCan = баллончик с краской
    .desc = Металлический баллончик для краски. Налейте в него любую цветную жидкость, чтобы рисовать ею.

ent-PaintCanRed = красная краска
    .desc = Баллончик густой красной краски.

ent-PaintCanYellow = жёлтая краска
    .desc = Баллончик густой жёлтой краски.

ent-PaintCanGreen = зелёная краска
    .desc = Баллончик густой зелёной краски.

ent-PaintCanBlue = синяя краска
    .desc = Баллончик густой синей краски.

ent-PaintCanViolet = фиолетовая краска
    .desc = Баллончик густой фиолетовой краски.

ent-PaintCanBlack = чёрная краска
    .desc = Баллончик густой чёрной краски.

ent-PaintCanWhite = белая краска
    .desc = Баллончик густой белой краски.

# Интерфейс и сообщения

guide-entry-painting = Живопись

paint-window-title = Холст
paint-window-tools = Инструменты
paint-window-brush = Стиль кисти
paint-window-size = Размер
paint-window-size-value = {$size}пикс.
paint-window-color = Цвет
paint-window-no-tool = Возьмите мелок или кисть, чтобы рисовать.
paint-window-undo = Отменить последний штрих (Ctrl+Z)
paint-window-finish = Завершить
paint-window-title-placeholder = Название
paint-window-finish-button = Завершить и подписать
paint-window-finish-confirm = Подписать? После этого изменить будет нельзя.
paint-window-finished = [bold]{$title}[/bold]
    Подписано: {$artist}

paint-tool-brush = Кисть
paint-tool-eraser = Ластик
paint-tool-line = Линия
paint-tool-rectangle = Прямоугольник
paint-tool-ellipse = Эллипс
paint-tool-picker = Пипетка

paint-style-round = Круглая
paint-style-square = Квадратная
paint-style-spray = Распыление
paint-style-calligraphy = Каллиграфия

paint-tool-empty = {CAPITALIZE(THE($tool))} полностью израсходован.

paint-untitled = Без названия
paint-unknown-artist = неизвестный художник
paint-finished-name = картина «{$title}»
paint-copy-name = копия «{$title}»
paint-copy-artist = {$artist} (копия)
paint-finished-popup = Вы подписываете «{$title}».
paint-examine-finished = Она называется [bold]{$title}[/bold], подписана: {$artist}.
paint-fax-use-photocopier = Факсимильные аппараты не могут работать с картинами. Используйте копировальный аппарат.

paint-slot-canvas = холст

paint-frame-unfinished = Завершите и подпишите картину, прежде чем вставлять её в рамку.
paint-frame-already-framed = У этой картины уже есть рамка.
paint-frame-applied = Вы вставляете {THE($painting)} в рамку.

paint-log-view-button = Посмотреть картину
paint-log-viewer-title = Картина {$token}
paint-log-viewer-missing = Эту картину не удалось найти. Возможно, она ещё не сохранена в базе данных.

cmd-paintloadurl-desc = Загружает изображение и растягивает его на поверхность для рисования, например на холст.
cmd-paintloadurl-help = Использование: paintloadurl <entity uid> <http(s) url>
cmd-paintloadurl-hint-target = <entity uid>
cmd-paintloadurl-hint-url = <http(s) url>
cmd-paintloadurl-bad-target = {$target} не является поверхностью для рисования.
cmd-paintloadurl-bad-url = URL должен быть абсолютным адресом http или https.
cmd-paintloadurl-downloading = Загрузка {$url}...
cmd-paintloadurl-too-large = Изображение больше 8 МБ.
cmd-paintloadurl-download-failed = Ошибка загрузки: {$error}
cmd-paintloadurl-bad-image = Не удалось прочитать изображение: {$error}
cmd-paintloadurl-success = Изображение загружено на {$target}.

reagent-desc-paint = Густая, ярко окрашенная краска. Налейте её в баллончик, чтобы рисовать.
reagent-name-paint-red = красная краска
reagent-name-paint-yellow = жёлтая краска
reagent-name-paint-green = зелёная краска
reagent-name-paint-blue = синяя краска
reagent-name-paint-violet = фиолетовая краска
reagent-name-paint-black = чёрная краска
reagent-name-paint-white = белая краска

photocopier-verb-copy = Скопировать картину
photocopier-busy = Он уже копирует.
photocopier-unpowered = У него нет питания.
photocopier-needs-canvases = Сначала положите картину и чистый холст.
photocopier-nothing-to-copy = Оба холста пусты.
photocopier-needs-blank = Один из холстов должен быть чистым и неподписанным.
photocopier-size-mismatch = Чистый холст должен быть того же размера, что и картина.
