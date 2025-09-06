// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.Interfaces;

namespace Logibooks.Core.RestModels;
public class MorphologySupportLevelDto
{
    public required int Level { get; set; }
    public required string Word { get; set; }
    public string Msg => ToString();
    public override string ToString()
    {
        if (Level == (int)MorphologySupportLevel.NoSupport)
            return $"Слово '{Word}' отсутствует в словаре системы " +
            "и не может быть использовано для поиска морфологического соотвествия. " +
            "Используйте точное соответствие";
        if (Level == (int)MorphologySupportLevel.FormsSupport)
            return $"Поиск однокоренных слов для '{Word}' не поддерживается словарём системы. " +
            "Используйте поиск форм слова.";
        return string.Empty;
    }
}

