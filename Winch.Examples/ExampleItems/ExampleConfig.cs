using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Winch.Util;

namespace ExampleItems;

[JsonObject]
public class ExampleConfig
{
    public bool Toggle = true; // "toggle" type
    public string Text = "Hello"; // "text" type
    public int Integer = 22; // "integer" type. Can be byte, short, int, long, sbyte, ushort, uint, or ulong
    public decimal Decimal = 1.01m; // "decimal" or "number" type. Can be float, double, or decimal
    public float Slider = 15; // "slider" type. Must be float
    public ExampleEnum Dropdown = ExampleEnum.Two; // "dropdown" type. Can be a string or an enum
    public DredgeColorTypeEnum Color = DredgeColorTypeEnum.NEUTRAL; // "color" type. Must be the DredgeColorTypeEnum enum.

    [JsonConverter(typeof(CustomStringEnumConverter))]
    public enum ExampleEnum
    {
        One,
        Two,
        Three
    }
}
