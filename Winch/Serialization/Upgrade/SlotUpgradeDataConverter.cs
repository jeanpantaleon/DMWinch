﻿using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Winch.Serialization.Upgrade;

public class SlotUpgradeDataConverter : UpgradeDataConverter
{
    private readonly Dictionary<string, FieldDefinition> _definitions = new()
    {
        { "cellGroupConfigs", new(new List<CellGroupConfiguration>(), o => DredgeTypeHelpers.ParseCellGroupConfigurations((JArray)o)) },
    };

    public SlotUpgradeDataConverter()
    {
        AddDefinitions(_definitions);
    }
}
