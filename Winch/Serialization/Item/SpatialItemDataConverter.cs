﻿using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Winch.Util;

// ReSharper disable HeapView.BoxingAllocation

namespace Winch.Serialization.Item;

public class SpatialItemDataConverter : ItemDataConverter
{
    private readonly Dictionary<string, FieldDefinition> _definitions = new()
    {
        { "itemTypeIcon", new(TextureUtil.GetSprite("JunkIcon"), null) },
        { "canBeSoldByPlayer", new(true, o => bool.Parse(o.ToString())) },
        { "canBeSoldInBulkAction", new(true, o => bool.Parse(o.ToString())) },
        { "value", new(decimal.Zero, o => decimal.Parse(o.ToString())) },
        { "hasSellOverride", new(false, o => bool.Parse(o.ToString())) },
        { "sellOverrideValue", new(decimal.Zero, o => decimal.Parse(o.ToString())) },
        { "sprite", new(TextureUtil.GetSprite("EmptyIcon"), o => TextureUtil.GetSprite(o.ToString())) },
        { "platformSpecificSpriteOverrides", new(null, null) },
        { "itemColor", new(new Color(0.1922f, 0.1922f, 0.1922f, 1f), o=> DredgeTypeHelpers.GetColorFromJsonObject(o)) }, // default game uses
        { "canBeDiscardedByPlayer", new(true, o => bool.Parse(o.ToString())) },
        { "canBeDiscardedDuringQuestPickup", new(true, o => bool.Parse(o.ToString())) },
        { "hasSpecialDiscardAction", new(false, o => bool.Parse(o.ToString())) },
        { "discardPromptOverride", new("", null) },
        { "showAlertOnDiscardHold", new(false, o => bool.Parse(o.ToString())) },
        { "discardHoldTimeOverride", new(false, o => bool.Parse(o.ToString())) },
        { "discardHoldTimeSec", new(0, o => float.Parse(o.ToString())) },
        { "damageMode", new(DamageMode.NONE, o=> DredgeTypeHelpers.GetEnumValue<DamageMode>(o)) },
        { "moveMode", new(MoveMode.FREE, o=> DredgeTypeHelpers.GetEnumValue<MoveMode>(o)) },
        { "ignoreDamageWhenPlacing", new(false, o => bool.Parse(o.ToString())) },
        { "isUnderlayItem", new(false, o => bool.Parse(o.ToString())) },
        { "forbidStorageTray", new(false, o => bool.Parse(o.ToString())) },
        { "dimensions", new(new List<Vector2Int>(){ new Vector2Int(0,0) }, o => DredgeTypeHelpers.ParseItemDimensions((JArray)o)) },
        { "squishFactor", new(0, o => float.Parse(o.ToString())) },
        { "itemOwnPrerequisites", new(new List<OwnedItemResearchablePrerequisite>(), o => DredgeTypeHelpers.ParseOwnedItemResearchablePrerequisites((JArray)o)) },
        { "researchPrerequisites", new(new List<ResearchedItemResearchablePrerequisite>(), o => DredgeTypeHelpers.ParseResearchedItemResearchablePrerequisites((JArray)o)) },
        { "researchPointsRequired", new(0, o => int.Parse(o.ToString())) },
        { "buyableWithoutResearch", new(false, o => bool.Parse(o.ToString())) },
        { "researchIsForRecipe", new(false, o => bool.Parse(o.ToString())) },
        { "useIntenseAberratedUIShader", new(false, o => bool.Parse(o.ToString())) }
    };

    public SpatialItemDataConverter()
    {
        AddDefinitions(_definitions);
    }
}
