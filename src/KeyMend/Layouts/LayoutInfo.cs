namespace KeyMend.Layouts;

/// <summary>Раскладка клавиатуры: описатель и двухбуквенный код языка для подсказки (EN, RU).</summary>
/// <param name="Hkl">Описатель раскладки (HKL).</param>
/// <param name="Code">Код языка в верхнем регистре; <c>??</c>, если язык не распознан.</param>
internal sealed record LayoutInfo(nint Hkl, string Code);

/// <summary>Раскладка вместе с окном переднего плана, которому она принадлежит: по смене окна различают переход между окнами и смену языка.</summary>
/// <param name="Window">Окно переднего плана верхнего уровня.</param>
/// <param name="Layout">Раскладка окна с фокусом.</param>
internal sealed record LayoutSnapshot(nint Window, LayoutInfo Layout);

/// <summary>Раскладки для исправления ввода: в какой набран текст и в какую его надо перепечатать.</summary>
/// <param name="Current">Текущая раскладка окна с фокусом: в ней набран текст.</param>
/// <param name="Target">Другая раскладка пары: в ней текст должен быть.</param>
internal sealed record LayoutPairResolution(LayoutInfo Current, LayoutInfo Target);
