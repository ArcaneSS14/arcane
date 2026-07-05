// MIT License

// Copyright (c) 2026 Vecortys (vecortys@gmail.com)

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Arcane.InfinityDorm;

[Prototype]
public sealed partial class InfinityDormPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Локализация названия дорматорий.
    /// </summary>
    [DataField(required: true)]
    public string Title = string.Empty;

    /// <summary>
    /// Локализация описания дорматорий.
    /// </summary>
    [DataField(required: true)]
    public string Description = string.Empty;

    /// <summary>
    /// Исходный автор грида.
    /// </summary>
    [DataField(required: true)]
    public string Author = string.Empty;

    /// <summary>
    /// Категория дорматория.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<InfinityDormCategoryPrototype> Category;

    /// <summary>
    /// Путь до целевого грида дорматория.
    /// </summary>
    [DataField(required: true)]
    public ResPath GridPath { get; private set; } = default!;
}

[Prototype]
public sealed partial class InfinityDormCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Цвет, в который будет окрашена кнопка.
    /// </summary>
    [DataField(required: true)]
    public Color Color;

    /// <summary>
    /// Название категории.
    /// </summary>
    [DataField(required: true)]
    public string Name = string.Empty;

    /// <summary>
    /// Приоритет, по которому будут сортироваться дорматории в UI. Больше - ниже категория.
    /// </summary>
    [DataField(required: true)]
    public int Priority;
}
