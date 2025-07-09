namespace Logibooks.Core.RestModels;

using Logibooks.Core.Models;

public class CountryCodeCompactDto
{
    public short IsoNumeric { get; set; }
    public string IsoAlpha2 { get; set; } = string.Empty;
    public string NameEnOfficial { get; set; } = string.Empty;
    public string NameRuOfficial { get; set; } = string.Empty;

    public CountryCodeCompactDto() {}
    public CountryCodeCompactDto(CountryCode cc)
    {
        IsoNumeric = cc.IsoNumeric;
        IsoAlpha2 = cc.IsoAlpha2;
        NameEnOfficial = cc.NameEnOfficial;
        NameRuOfficial = cc.NameRuOfficial;
    }
}
