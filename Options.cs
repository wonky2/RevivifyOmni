using Menu.Remix.MixedUI;
using Menu.Remix.MixedUI.ValueTypes;
using System.Collections.Generic;
using UnityEngine;

namespace RevivifyOmni;

sealed class Options : OptionInterface
{
    // common
    public static Configurable<string> Mode;
    public static Configurable<int> DeathsUntilExhaustion;
    public static Configurable<int> DeathsUntilComa;
    public static Configurable<bool> DisableExhaustion;
    public static Configurable<int> DeathsUntilExpire;
    public static Configurable<float> CorpseExpiryTime;
    public static Configurable<bool> DisableExpiry;
    public static Configurable<bool> DisableInArena;
    // cpr
    public static Configurable<float> ReviveSpeed;
    // proximity
    public static Configurable<float> ProximityDistance;
    public static Configurable<int> ProximityTime;

    // extras
    public static Configurable<bool> DisableRPC;
    public static Configurable<bool> DebugMode;
    public static Configurable<bool> ShowSettingsInPauseMenu;
    public static Configurable<bool> DisableSlugpupSwapCorpseWithItem;

    public Options()
    {
        // common
        Mode = config.Bind("cfgMode", "C");
        DeathsUntilExhaustion = config.Bind("cfgDeathsUntilExhaustion", 1, new ConfigAcceptableRange<int>(1, 10));
        DeathsUntilComa = config.Bind("cfgDeathsUntilComa", 2, new ConfigAcceptableRange<int>(1, 10));
        DisableExhaustion = config.Bind("cfgDisableExhaustion", false);
        DeathsUntilExpire = config.Bind("cfgDeathsUntilExpire", 3, new ConfigAcceptableRange<int>(1, 10));
        CorpseExpiryTime = config.Bind("cfgCorpseExpiryTime", 1.5f, new ConfigAcceptableRange<float>(0.05f, 10f));
        DisableExpiry = config.Bind("cfgDisableExpiry", false);
        DisableInArena = config.Bind("cfgDisableInArena", true);
        // cpr
        ReviveSpeed = config.Bind("cfgReviveSpeed", 1f, new ConfigAcceptableRange<float>(0.1f, 5f));
        // proximity
        ProximityDistance = config.Bind("cfgProximityDistance", 60f, new ConfigAcceptableRange<float>(20f, 120f));
        ProximityTime = config.Bind("cfgProximityTime", 5, new ConfigAcceptableRange<int>(1, 10));

        // extras
        DisableRPC = config.Bind("cfgDisableRPC", false);
        DebugMode = config.Bind("cfgDebugMode", false);
        ShowSettingsInPauseMenu = config.Bind("cfgShowSettingsInPauseMenu", true);
        DisableSlugpupSwapCorpseWithItem = config.Bind("cfgDisableSlugpupSwapCorpseWithItem", true);
    }

    private UIelement[] UIOptions;
    private UIelement[] UIExtras;

    private readonly List<ListItem> ModeList = new()
    {
        new ListItem
        {
            name = "C",
            displayName = "CPR",
            value = 0,
            desc = "Perform chest compressions to revive"
        },

        new ListItem
        {
            name = "P",
            displayName = "Proximity",
            value = 1,
            desc = "Just be nearby to revive"
        },

        new ListItem
        {
            name = "N",
            displayName = "None",
            value = 2,
            desc = "Disable reviving completely"
        }
    };

    public override void Initialize()
    {
        var opCommon = new OpTab(this, "Settings");
        var opExtras = new OpTab(this, "Extras");

        Tabs = new[]
        {
            opCommon,
            opExtras
        };

        float sliderX = 270;
        float y = 570;

        UIOptions = new UIelement[]
        {
            new OpLabel(new(220, y), Vector2.zero, "Revivify mode", FLabelAlignment.Right),
            new OpComboBox(Mode, new Vector2(sliderX, y - 6), 160, ModeList),

            // commmon
            new OpLabel(new(280, y -= 50 + 60), Vector2.zero, "Common settings", FLabelAlignment.Center, true),

            new OpLabel(new(220, y -= 30), Vector2.zero, "Deaths until exhaustion", FLabelAlignment.Right),
            new OpSlider(DeathsUntilExhaustion, new Vector2(sliderX, y - 6), 300),

            new OpLabel(new(220, y -= 30), Vector2.zero, "Deaths until slugpups become comatose", FLabelAlignment.Right),
            new OpSlider(DeathsUntilComa, new Vector2(sliderX, y - 6), 300),

            new OpLabel(new(220, y -= 30), Vector2.zero, "Disable exhaustion and comatose slugpups", FLabelAlignment.Right),
            new OpCheckBox(DisableExhaustion, new Vector2(sliderX, y - 6)),


            new OpLabel(new(220, y -= 50), Vector2.zero, "Deaths until slugpups permanently expire", FLabelAlignment.Right),
            new OpSlider(DeathsUntilExpire, new Vector2(sliderX, y - 6), 300),

            new OpLabel(new(220, y -= 30), Vector2.zero, "Time until bodies expire, in minutes", FLabelAlignment.Right),
            new OpFloatSlider(CorpseExpiryTime, new Vector2(sliderX, y - 6), 300),

            new OpLabel(new(220, y -= 30), Vector2.zero, "Disable expiration", FLabelAlignment.Right),
            new OpCheckBox(DisableExpiry, new Vector2(sliderX, y - 6)),


            new OpLabel(new(220, y -= 50), Vector2.zero, "Disable reviving in Arena", FLabelAlignment.Right),
            new OpCheckBox(DisableInArena, new Vector2(sliderX, y - 6)),

            // cpr
            new OpLabel(new(280, y -= 50), Vector2.zero, "CPR only", FLabelAlignment.Center, true),

            new OpLabel(new(220, y -= 30), Vector2.zero, "Revive speed multiplier", FLabelAlignment.Right),
            new OpFloatSlider(ReviveSpeed, new Vector2(sliderX, y - 6), 300),

            // proximity
            new OpLabel(new(280, y -= 50), Vector2.zero, "Proximity only", FLabelAlignment.Center, true),

            new OpLabel(new(220, y -= 30), Vector2.zero, "Distance from nearest alive slugcat", FLabelAlignment.Right),
            new OpFloatSlider(ProximityDistance, new Vector2(sliderX, y - 6), 300, decimalNum : 0),

            new OpLabel(new(220, y -= 30), Vector2.zero, "Time until revived", FLabelAlignment.Right),
            new OpSlider(ProximityTime, new Vector2(sliderX, y - 6), 300),
        };
        opCommon.AddItems(UIOptions);

        sliderX = 270;
        y = 570;

        UIExtras = new UIelement[]
        {
            new OpLabel(new(220, y), Vector2.zero, "Client-side reviving only", FLabelAlignment.Right),
            new OpCheckBox(DisableRPC, new Vector2(sliderX, y - 6)),

            new OpLabel(new(220, y -= 30), Vector2.zero, "Debug Mode", FLabelAlignment.Right),
            new OpCheckBox(DebugMode, new Vector2(sliderX, y - 6)),

            new OpLabel(new(220, y -= 30), Vector2.zero, "Show Revivify settings in pause menu", FLabelAlignment.Right),
            new OpCheckBox(ShowSettingsInPauseMenu, new Vector2(sliderX, y - 6)),

            new OpLabel(new(220, y -= 30), Vector2.zero, "Prevent player slugpups from swapping corpse with item outside CPR mode", FLabelAlignment.Right),
            new OpCheckBox(DisableSlugpupSwapCorpseWithItem, new Vector2(sliderX, y - 6)),
        };
        opExtras.AddItems(UIExtras);
    }

    readonly Color active = new(0.663f, 0.643f, 0.698f);
    readonly Color inactive = new(0.294f, 0.275f, 0.325f);

    public override void Update()
    {
        // mode option colors
        if (((OpComboBox)UIOptions[1]).value == "N")
        {
            for (int i = 2; i < UIOptions.Length; i++)
            {
                if (UIOptions[i] is OpLabel label)
                {
                    label.color = inactive;
                }
                else if (UIOptions[i] is OpSlider slider)
                {
                    slider.colorEdge = inactive;
                    slider.colorLine = inactive;
                }
                else if (UIOptions[i] is OpFloatSlider floatSlider)
                {
                    floatSlider.colorEdge = inactive;
                    floatSlider.colorLine = inactive;
                }
                else if (UIOptions[i] is OpCheckBox checkBox)
                {
                    checkBox.colorEdge = inactive;
                }
            }
            return;
        }
        else
        {
            for (int i = 2; i < UIOptions.Length; i++)
            {
                if (UIOptions[i] is OpLabel label)
                {
                    label.color = active;
                }
                else if (UIOptions[i] is OpSlider slider)
                {
                    slider.colorEdge = active;
                    slider.colorLine = active;
                }
                else if (UIOptions[i] is OpFloatSlider floatSlider)
                {
                    floatSlider.colorEdge = active;
                    floatSlider.colorLine = active;
                }
                else if (UIOptions[i] is OpCheckBox checkBox)
                {
                    checkBox.colorEdge = active;
                }
            }
        }
        if (((OpComboBox)UIOptions[1]).value != "C")
        {
            ((OpLabel)UIOptions[17]).color = inactive;
            ((OpLabel)UIOptions[18]).color = inactive;
            ((OpFloatSlider)UIOptions[19]).colorEdge = inactive;
            ((OpFloatSlider)UIOptions[19]).colorLine = inactive;
        }
        else
        {
            ((OpLabel)UIOptions[17]).color = active;
            ((OpLabel)UIOptions[18]).color = active;
            ((OpFloatSlider)UIOptions[19]).colorEdge = active;
            ((OpFloatSlider)UIOptions[19]).colorLine = active;
        }
        if (((OpComboBox)UIOptions[1]).value != "P")
        {
            ((OpLabel)UIOptions[20]).color = inactive;
            ((OpLabel)UIOptions[21]).color = inactive;
            ((OpFloatSlider)UIOptions[22]).colorEdge = inactive;
            ((OpFloatSlider)UIOptions[22]).colorLine = inactive;
            ((OpLabel)UIOptions[23]).color = inactive;
            ((OpSlider)UIOptions[24]).colorEdge = inactive;
            ((OpSlider)UIOptions[24]).colorLine = inactive;
        }
        else
        {
            ((OpLabel)UIOptions[20]).color = active;
            ((OpLabel)UIOptions[21]).color = active;
            ((OpFloatSlider)UIOptions[22]).colorEdge = active;
            ((OpFloatSlider)UIOptions[22]).colorLine = active;
            ((OpLabel)UIOptions[23]).color = active;
            ((OpSlider)UIOptions[24]).colorEdge = active;
            ((OpSlider)UIOptions[24]).colorLine = active;
        }

        // exhaustion option colors
        if (((OpCheckBox)UIOptions[8]).GetValueBool())
        {
            ((OpLabel)UIOptions[3]).color = inactive;
            ((OpSlider)UIOptions[4]).colorEdge = inactive;
            ((OpSlider)UIOptions[4]).colorLine = inactive;
            ((OpLabel)UIOptions[5]).color = inactive;
            ((OpSlider)UIOptions[6]).colorEdge = inactive;
            ((OpSlider)UIOptions[6]).colorLine = inactive;
        }
        else
        {
            ((OpLabel)UIOptions[3]).color = active;
            ((OpSlider)UIOptions[4]).colorEdge = active;
            ((OpSlider)UIOptions[4]).colorLine = active;
            ((OpLabel)UIOptions[5]).color = active;
            ((OpSlider)UIOptions[6]).colorEdge = active;
            ((OpSlider)UIOptions[6]).colorLine = active;
        }

        // coma option colors
        if (((OpCheckBox)UIOptions[14]).GetValueBool())
        {
            ((OpLabel)UIOptions[9]).color = inactive;
            ((OpSlider)UIOptions[10]).colorEdge = inactive;
            ((OpSlider)UIOptions[10]).colorLine = inactive;
            ((OpLabel)UIOptions[11]).color = inactive;
            ((OpFloatSlider)UIOptions[12]).colorEdge = inactive;
            ((OpFloatSlider)UIOptions[12]).colorLine = inactive;
        }
        else
        {
            ((OpLabel)UIOptions[9]).color = active;
            ((OpSlider)UIOptions[10]).colorEdge = active;
            ((OpSlider)UIOptions[10]).colorLine = active;
            ((OpLabel)UIOptions[11]).color = active;
            ((OpFloatSlider)UIOptions[12]).colorEdge = active;
            ((OpFloatSlider)UIOptions[12]).colorLine = active;
        }
    }
}
