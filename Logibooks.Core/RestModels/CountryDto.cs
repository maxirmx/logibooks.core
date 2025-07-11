namespace Logibooks.Core.RestModels;

using Logibooks.Core.Models;

public class CountryDto
{
    public short IsoNumeric { get; set; }
    public string IsoAlpha2 { get; set; } = string.Empty;
    public string NameEnShort { get; set; } = string.Empty;
    public string NameEnFormal { get; set; } = string.Empty;
    public string NameEnOfficial { get; set; } = string.Empty;
    public string NameEnCldr { get; set; } = string.Empty;
    public string NameRuShort { get; set; } = string.Empty;
    public string NameRuFormal { get; set; } = string.Empty;
    public string NameRuOfficial { get; set; } = string.Empty;
    public DateTime LoadedAt { get; set; }

    public CountryDto() {}
    public CountryDto(Country cc)
    {
        IsoNumeric = cc.IsoNumeric;
        IsoAlpha2 = cc.IsoAlpha2;
        NameEnShort = cc.NameEnShort;
        NameEnFormal = cc.NameEnFormal;
        NameEnOfficial = cc.NameEnOfficial;
        NameEnCldr = cc.NameEnCldr;
        NameRuShort = cc.NameRuShort;
        NameRuFormal = cc.NameRuFormal;
        NameRuOfficial = cc.NameRuOfficial;
        LoadedAt = cc.LoadedAt;
    }
}
