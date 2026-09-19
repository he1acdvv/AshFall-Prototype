using System.Linq;
using System.Numerics;
using Content.Shared.Ashfall.CharacterGen.Prototypes;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.Traits;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Content.Shared.Dataset;

namespace Content.Shared.Ashfall.CharacterGen;

/// <summary>
///     Pure generator producing realistic, data-driven HumanoidCharacterProfiles according to constraint prototypes.
/// </summary>
public sealed class AshfallCharacterGenerator
{
    private static readonly ProtoId<ColorPalettePrototype> GrayingHairPalette = "HumanHairGraying";

    /// <summary>
    ///     Really short Veiru hairstyles, tinted to match the base coat.
    /// </summary>
    private static readonly HashSet<string> ShortVeiruHair = new()
    {
        "VeiruHairClean",
        "VeiruHairBob",
        "VeiruHairBedhead",
        "VeiruHairMohawk",
        "VeiruHairBangs",
        "VeiruHairCombedback",
        "VeiruHairPunk",
        "VeiruHairTough",
        "VeiruSpikey",
    };

    private readonly IPrototypeManager _prototypeManager;
    private readonly MarkingManager _markingManager;
    private readonly NamingSystem _namingSystem;

    public AshfallCharacterGenerator(
        IPrototypeManager prototypeManager,
        MarkingManager markingManager,
        NamingSystem namingSystem)
    {
        _prototypeManager = prototypeManager;
        _markingManager = markingManager;
        _namingSystem = namingSystem;
    }

    /// <summary>
    ///     Picks sex, gender and age. Culture, naming and the career structure are handled by
    ///     <see cref="AshfallPersonGenerator"/>; this part is purely visual identity.
    /// </summary>
    public (Sex Sex, Gender Gender, int Age) GenerateIdentity(CharacterGenConstraintsPrototype constraints, IRobustRandom random)
    {
        var species = _prototypeManager.Index(constraints.Species);
        var sex = random.Pick(species.Sexes);
        var gender = sex switch
        {
            Sex.Female => Gender.Female,
            Sex.Male => Gender.Male,
            _ => Gender.Epicene
        };
        var age = PickAge(constraints.AgeBrackets, random);
        return (sex, gender, age);
    }

    /// <summary>
    ///     Generates natural appearance constrained to the species, decoupled from culture.
    /// </summary>
    public (HumanoidCharacterAppearance Appearance, string Morphology) GenerateAppearance(
        CharacterGenConstraintsPrototype constraints,
        SpeciesPrototype species,
        Sex sex,
        int age,
        IRobustRandom random,
        AshfallCulturePrototype? culture = null)
    {
        Color skinColor;
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> markings = new();
        string morphology = string.Empty;
        float tone = 25f;

        if (species.ID == "Reptilian")
        {
            (skinColor, morphology) = GenerateReptilianSkin(sex, random);
        }
        else if (species.ID == "Moth")
        {
            (skinColor, morphology) = GenerateLuamSkin(random);
        }
        else if (species.ID == "Arachnid")
        {
            (skinColor, morphology) = GenerateArachnidSkin(random);
        }
        else if (species.ID == "Veiru")
        {
            (skinColor, morphology) = GenerateVeiruSkin(random);
        }
        else
        {
            (skinColor, tone) = GenerateNaturalSkinColor(species, culture, random);
        }

        var hairColor = GenerateHairColor(constraints, age, tone, random);
        var eyeColor = GenerateEyeColor(constraints, random);

        if (species.ID is "Reptilian" or "Moth" or "Arachnid" or "Veiru")
        {
            markings = GenerateSpeciesMarkings(species, sex, skinColor, eyeColor, hairColor, random);
        }
        else
        {
            markings = GenerateMarkings(species, sex, hairColor, eyeColor, skinColor, constraints, culture, random);
        }

        var appearance = new HumanoidCharacterAppearance
        {
            SkinColor = skinColor,
            EyeColor = eyeColor,
            Markings = markings
        };

        return (appearance, morphology);
    }

    /// <summary>
    ///     Assembles the final profile from the generated identity, name and appearance.
    /// </summary>
    public HumanoidCharacterProfile BuildProfile(
        string name,
        SpeciesPrototype species,
        Sex sex,
        Gender gender,
        int age,
        HumanoidCharacterAppearance appearance,
        IRobustRandom random)
    {
        var voice = species.DefaultSoundsBySex.Length > (int)sex
            ? species.DefaultSoundsBySex[(int)sex]
            : species.Voices.FirstOrDefault();

        var traits = new HashSet<ProtoId<TraitPrototype>>();
        var pool = new ProtoId<TraitPrototype>[]
        {
            "AshfallTraitShipyardVeteran",
            "AshfallTraitCorporatePedant",
            "AshfallTraitNightShift",
            "AshfallTraitVacuumAccustomed",
            "AshfallTraitSalvageResilience",
            "AshfallTraitServiceServiceability",
            "AshfallTraitAgroGreenThumb",
            "AshfallTraitBureaucraticPatience",
            "Pacifist",
            "LightweightDrunk",
            "Snoring"
        };
        var count = random.Pick(new[] { 1, 1, 2 });
        for (var i = 0; i < count; i++)
        {
            var traitId = random.Pick(pool);
            if (_prototypeManager.HasIndex(traitId))
                traits.Add(traitId);
        }

        return new HumanoidCharacterProfile(
            name,
            string.Empty,
            species.ID,
            age,
            sex,
            voice,
            gender,
            appearance,
            SpawnPriorityPreference.Cryosleep,
            new Dictionary<ProtoId<JobPrototype>, JobPriority>(),
            PreferenceUnavailableMode.SpawnAsOverflow,
            new HashSet<ProtoId<AntagPrototype>>(),
            traits,
            new Dictionary<string, RoleLoadout>()
        );
    }

    private static int PickAge(List<AgeWeightBracket> brackets, IRobustRandom random)
    {
        if (brackets.Count == 0)
            return random.Next(18, 65);

        var totalWeight = 0f;
        foreach (var b in brackets)
            totalWeight += b.Weight;

        var roll = random.NextFloat(0f, totalWeight);
        var current = 0f;

        foreach (var b in brackets)
        {
            current += b.Weight;
            if (roll <= current)
                return random.Next(b.MinAge, b.MaxAge + 1);
        }

        var last = brackets[^1];
        return random.Next(last.MinAge, last.MaxAge + 1);
    }

    private (Color Color, float Tone) GenerateNaturalSkinColor(SpeciesPrototype species, AshfallCulturePrototype? culture, IRobustRandom random)
    {
        if (_prototypeManager.TryIndex(species.SkinColoration, out var colorationProto))
        {
            var strategy = colorationProto.Strategy;
            if (strategy is HumanTonedSkinColoration toned)
            {
                float tone;
                if (culture?.SkinToneRange != null)
                {
                    var range = culture.SkinToneRange.Value;
                    tone = random.NextFloat(range.X, range.Y);
                }
                else
                {
                    // Unary tone: 0 to 100 with realistic human melanin distribution
                    // ~55% light/fair (3..35), ~30% medium/tanned (35..65), ~15% dark/deep (65..95)
                    var roll = random.NextFloat(0f, 1f);
                    tone = roll switch
                    {
                        < 0.55f => random.NextFloat(3f, 35f),
                        < 0.85f => random.NextFloat(35f, 65f),
                        _ => random.NextFloat(65f, 95f)
                    };
                }
                return (toned.FromUnary(tone), tone);
            }
        }

        // Fallback natural skin tone
        return (Color.FromHsv(new Vector4(25f / 360f, random.NextFloat(0.25f, 0.65f), random.NextFloat(0.50f, 0.95f), 1f)), 25f);
    }

    private Color GenerateHairColor(CharacterGenConstraintsPrototype constraints, int age, float tone, IRobustRandom random)
    {
        // Check graying chance based on age
        var grayChance = 0.01f;
        foreach (var check in constraints.GrayingChances)
        {
            if (age <= check.MaxAge)
            {
                grayChance = check.Chance;
                break;
            }
        }

        if (random.Prob(grayChance))
        {
            if (_prototypeManager.TryIndex(GrayingHairPalette, out var grayPalette))
                return SamplePalette(grayPalette, random, false);
        }

        if (_prototypeManager.TryIndex(constraints.HairPalette, out var hairPalette))
        {
            // For primarily darker skin tones (melanin tone >= 55), exclude full blonde hair
            var disallowBlonde = tone >= 55f;
            return SamplePalette(hairPalette, random, disallowBlonde);
        }

        return Color.FromHex("#3D2E24"); // Natural dark brown fallback
    }

    private Color GenerateEyeColor(CharacterGenConstraintsPrototype constraints, IRobustRandom random)
    {
        if (_prototypeManager.TryIndex(constraints.EyePalette, out var eyePalette))
            return SamplePalette(eyePalette, random);

        return Color.FromHex("#452C1A"); // Natural brown fallback
    }

    private static Color SamplePalette(ColorPalettePrototype palette, IRobustRandom random, bool disallowBlonde = false)
    {
        var colors = palette.Colors;
        if (disallowBlonde)
        {
            var filtered = colors.Where(c => !c.Name.Contains("blonde", StringComparison.OrdinalIgnoreCase)).ToList();
            if (filtered.Count > 0)
                colors = filtered;
        }

        if (colors.Count == 0)
            return Color.Black;

        var totalWeight = 0f;
        foreach (var c in colors)
            totalWeight += c.Weight;

        var roll = random.NextFloat(0f, totalWeight);
        var current = 0f;
        var selectedColor = colors[0].Color;

        foreach (var c in colors)
        {
            current += c.Weight;
            if (roll <= current)
            {
                selectedColor = c.Color;
                break;
            }
        }

        // Apply bounded channel jitter
        var jitter = palette.ChannelJitter;
        var r = Math.Clamp(selectedColor.R + random.NextFloat(-jitter, jitter), palette.MinValue, palette.MaxValue);
        var g = Math.Clamp(selectedColor.G + random.NextFloat(-jitter, jitter), palette.MinValue, palette.MaxValue);
        var b = Math.Clamp(selectedColor.B + random.NextFloat(-jitter, jitter), palette.MinValue, palette.MaxValue);

        return new Color(r, g, b, 1f);
    }

    private Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> GenerateMarkings(
        SpeciesPrototype species,
        Sex sex,
        Color hairColor,
        Color eyeColor,
        Color skinColor,
        CharacterGenConstraintsPrototype constraints,
        AshfallCulturePrototype? culture,
        IRobustRandom random)
    {
        var markings = new Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>>();
        var headCategory = new ProtoId<OrganCategoryPrototype>("Head");
        var headLayers = new Dictionary<HumanoidVisualLayers, List<Marking>>();

        // 1. Hair Marking
        var hairMarkings = _markingManager.MarkingsByLayerAndGroupAndSex(
            HumanoidVisualLayers.Hair,
            new ProtoId<MarkingsGroupPrototype>("Human"),
            sex);

        var allRestrictedHairs = GetRestrictedCultureHairstyles();
        var cultureAllowedHairs = culture != null && culture.CultureHairstyles.Count > 0
            ? new HashSet<string>(culture.CultureHairstyles.Select(h => (string) h))
            : null;

        var validHairPool = new List<MarkingPrototype>();
        var cultureHairPool = new List<MarkingPrototype>();

        foreach (var hair in hairMarkings.Values)
        {
            if (constraints.AllowedHairstyles.Count > 0 && !constraints.AllowedHairstyles.Contains(hair.ID))
                continue;

            bool isRestricted = allRestrictedHairs.Contains(hair.ID);
            if (isRestricted)
            {
                if (cultureAllowedHairs != null && cultureAllowedHairs.Contains(hair.ID))
                {
                    cultureHairPool.Add(hair);
                    validHairPool.Add(hair);
                }
            }
            else
            {
                validHairPool.Add(hair);
            }
        }

        if (cultureHairPool.Count > 0 && random.Prob(0.35f))
        {
            var chosenHair = PickWeightedMarking(cultureHairPool, random);
            headLayers[HumanoidVisualLayers.Hair] = new List<Marking> { new(chosenHair.ID, [hairColor]) };
        }
        else if (validHairPool.Count > 0)
        {
            var chosenHair = PickWeightedMarking(validHairPool, random);
            headLayers[HumanoidVisualLayers.Hair] = new List<Marking> { new(chosenHair.ID, [hairColor]) };
        }
        else
        {
            headLayers[HumanoidVisualLayers.Hair] = new List<Marking> { new(HairStyles.DefaultHairStyle, [hairColor]) };
        }

        // 2. Facial Hair Marking (Males only, 40% chance of beard/mustache, 60% clean shaven)
        if (sex == Sex.Male)
        {
            var facialHairMarkings = _markingManager.MarkingsByLayerAndGroupAndSex(
                HumanoidVisualLayers.FacialHair,
                new ProtoId<MarkingsGroupPrototype>("Human"),
                sex);

            var validFacialPool = new List<MarkingPrototype>();
            foreach (var fHair in facialHairMarkings.Values)
            {
                if (fHair.ID == HairStyles.DefaultFacialHairStyle)
                    continue;

                if (constraints.AllowedFacialHairstyles.Count > 0 && !constraints.AllowedFacialHairstyles.Contains(fHair.ID))
                    continue;

                validFacialPool.Add(fHair);
            }

            if (validFacialPool.Count > 0 && random.Prob(0.40f))
            {
                var chosenFacial = PickWeightedMarking(validFacialPool, random);
                // Facial hair color matches hair color with tiny variation
                var beardColor = hairColor
                    .WithRed(Math.Clamp(hairColor.R + random.NextFloat(-0.01f, 0.01f), 0.05f, 0.95f))
                    .WithGreen(Math.Clamp(hairColor.G + random.NextFloat(-0.01f, 0.01f), 0.05f, 0.95f))
                    .WithBlue(Math.Clamp(hairColor.B + random.NextFloat(-0.01f, 0.01f), 0.05f, 0.95f));

                headLayers[HumanoidVisualLayers.FacialHair] = new List<Marking> { new(chosenFacial.ID, [beardColor]) };
            }
            else
            {
                headLayers[HumanoidVisualLayers.FacialHair] = new List<Marking> { new(HairStyles.DefaultFacialHairStyle, [hairColor]) };
            }
        }

        markings[headCategory] = headLayers;
        return markings;
    }

    private HashSet<string> GetRestrictedCultureHairstyles()
    {
        var set = new HashSet<string>();
        foreach (var c in _prototypeManager.EnumeratePrototypes<AshfallCulturePrototype>())
        {
            foreach (var hairId in c.CultureHairstyles)
            {
                set.Add((string) hairId);
            }
        }
        return set;
    }

    private static MarkingPrototype PickWeightedMarking(List<MarkingPrototype> pool, IRobustRandom random)
    {
        var totalWeight = 0f;
        foreach (var m in pool)
            totalWeight += m.RandomWeight;

        if (totalWeight <= 0)
            return random.Pick(pool);

        var roll = random.NextFloat(0f, totalWeight);
        var current = 0f;

        foreach (var m in pool)
        {
            current += m.RandomWeight;
            if (roll <= current)
                return m;
        }

        return pool[0];
    }

    private (Color SkinColor, string Morphology) GenerateReptilianSkin(Sex sex, IRobustRandom random)
    {
        var morphs = new[] { "Ла'шар", "Ссаир", "Кхареш", "Тарраш" };
        var morphology = random.Pick(morphs);

        Color skinColor;
        switch (morphology)
        {
            case "Ла'шар": // Aquatic / coastal
                skinColor = random.Pick(new[]
                {
                    Color.FromHex("#4E6068"),
                    Color.FromHex("#3B4D54"),
                    Color.FromHex("#5B727A"),
                    Color.FromHex("#485A59"),
                    Color.FromHex("#2F3F46"),
                    Color.FromHex("#506863")
                });
                break;

            case "Ссаир": // Serpentine
                skinColor = random.Pick(new[]
                {
                    Color.FromHex("#8C7A5E"),
                    Color.FromHex("#6B5E43"),
                    Color.FromHex("#594C38"),
                    Color.FromHex("#4A523A"),
                    Color.FromHex("#383B36"),
                    Color.FromHex("#78573C")
                });
                break;

            case "Кхареш": // Longsnout
                skinColor = random.Pick(new[]
                {
                    Color.FromHex("#7A4D32"),
                    Color.FromHex("#8A5E3B"),
                    Color.FromHex("#663B26"),
                    Color.FromHex("#4D5233"),
                    Color.FromHex("#5E442B"),
                    Color.FromHex("#6B5238")
                });
                break;

            case "Тарраш": // Crested
            default:
                skinColor = random.Pick(new[]
                {
                    Color.FromHex("#8C452B"),
                    Color.FromHex("#825E2B"),
                    Color.FromHex("#786926"),
                    Color.FromHex("#485731"),
                    Color.FromHex("#3B3D3B"),
                    Color.FromHex("#6E4B31")
                });
                break;
        }

        return (skinColor, morphology);
    }

    private (Color SkinColor, string Morphology) GenerateLuamSkin(IRobustRandom random)
    {
        var morphs = new[] { "Кхаар", "Сеир", "Векк", "Дуур", "Шиал" };
        var morphology = random.Pick(morphs);

        Color skinColor;
        switch (morphology)
        {
            case "Кхаар": // Large fluffy atlas/luna
                skinColor = random.Pick(new[]
                {
                    Color.FromHex("#D0C8B0"),
                    Color.FromHex("#708B75"),
                    Color.FromHex("#B09875"),
                    Color.FromHex("#B58B8D")
                });
                break;

            case "Сеир": // Hawkmoth
                skinColor = random.Pick(new[]
                {
                    Color.FromHex("#5E6352"),
                    Color.FromHex("#4E524D"),
                    Color.FromHex("#635147"),
                    Color.FromHex("#593B3E")
                });
                break;

            case "Векк": // Tiger moth warning colors
                skinColor = random.Pick(new[]
                {
                    Color.FromHex("#2B2827"),
                    Color.FromHex("#C8983E"),
                    Color.FromHex("#B85A32"),
                    Color.FromHex("#94372B")
                });
                break;

            case "Дуур": // Noctuid / cryptic bark
                skinColor = random.Pick(new[]
                {
                    Color.FromHex("#3D3B39"),
                    Color.FromHex("#4F473F"),
                    Color.FromHex("#2B2B2B"),
                    Color.FromHex("#5B574F")
                });
                break;

            case "Шиал": // Glasswing / pale cold
            default:
                skinColor = random.Pick(new[]
                {
                    Color.FromHex("#E0E5E5"),
                    Color.FromHex("#C2CECE"),
                    Color.FromHex("#A8B5B8"),
                    Color.FromHex("#8FA3A8")
                });
                break;
        }

        return (skinColor, morphology);
    }

    private (Color SkinColor, string Morphology) GenerateArachnidSkin(IRobustRandom random)
    {
        var morphs = new[] { "Оррак", "Тесс", "Вейр", "Керр", "Нерр" };
        var morphology = random.Pick(morphs);

        Color skinColor;
        switch (morphology)
        {
            case "Оррак":
                skinColor = random.Pick(new[] { Color.FromHex("#3B2F2F"), Color.FromHex("#2A2421"), Color.FromHex("#5E3E2B"), Color.FromHex("#2B1E1E") });
                break;
            case "Тесс":
                skinColor = random.Pick(new[] { Color.FromHex("#1F1F1F"), Color.FromHex("#8C7A6B"), Color.FromHex("#4F4D3F"), Color.FromHex("#332D2D") });
                break;
            case "Вейр":
                skinColor = random.Pick(new[] { Color.FromHex("#7A684C"), Color.FromHex("#8C7338"), Color.FromHex("#594C38"), Color.FromHex("#6B433B") });
                break;
            case "Керр":
                skinColor = random.Pick(new[] { Color.FromHex("#4D4A43"), Color.FromHex("#3D3833"), Color.FromHex("#59524A"), Color.FromHex("#6B5F52") });
                break;
            case "Нерр":
            default:
                skinColor = random.Pick(new[] { Color.FromHex("#73736E"), Color.FromHex("#8C8C85"), Color.FromHex("#595954"), Color.FromHex("#40403C") });
                break;
        }

        return (skinColor, morphology);
    }

    private (Color SkinColor, string Morphology) GenerateVeiruSkin(IRobustRandom random)
    {
        var morphs = new[] { "Раав", "Меир", "Керр", "Ваир" };
        var morphology = random.Pick(morphs);

        Color skinColor;
        switch (morphology)
        {
            case "Раав":
                skinColor = random.Pick(new[]
                {
                    Color.FromHex("#9E8B6E"),
                    Color.FromHex("#73644F"),
                    Color.FromHex("#8A5E3E"),
                    Color.FromHex("#40362C"),
                    Color.FromHex("#1F1C19")
                });
                break;

            case "Меир":
                skinColor = random.Pick(new[]
                {
                    Color.FromHex("#787C80"),
                    Color.FromHex("#9DA3A8"),
                    Color.FromHex("#C7CDD4"),
                    Color.FromHex("#42474C"),
                    Color.FromHex("#5E4B3E")
                });
                break;

            case "Керр":
                skinColor = random.Pick(new[]
                {
                    Color.FromHex("#59524A"),
                    Color.FromHex("#705943"),
                    Color.FromHex("#8C5438"),
                    Color.FromHex("#38322D")
                });
                break;

            case "Ваир":
            default:
                skinColor = random.Pick(new[]
                {
                    Color.FromHex("#C9B08B"),
                    Color.FromHex("#9E8059"),
                    Color.FromHex("#B56E3C"),
                    Color.FromHex("#4F473E")
                });
                break;
        }

        return (skinColor, morphology);
    }

    /// <summary>
    ///     Builds species markings fully from prototypes: organ layers and groups come from the body,
    ///     pools from the markings group, mandatory/decorative from group limits.
    /// </summary>
    private Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> GenerateSpeciesMarkings(
        SpeciesPrototype species,
        Sex sex,
        Color skinColor,
        Color eyeColor,
        Color hairColor,
        IRobustRandom random)
    {
        var markingData = _markingManager.GetMarkingData(species.ID);
        var result = new Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>>();

        foreach (var (organ, organData) in markingData)
        {
            if (!_prototypeManager.TryIndex(organData.Group, out var groupProto))
                continue;

            var layerMarkings = new Dictionary<HumanoidVisualLayers, List<Marking>>();

            foreach (var layer in organData.Layers)
            {
                // Veiru do not grow facial hair.
                if (species.ID == "Veiru" && layer == HumanoidVisualLayers.FacialHair)
                    continue;

                if (!groupProto.Limits.TryGetValue(layer, out var limits) || limits.Limit <= 0)
                    continue;

                var pool = _markingManager.MarkingsByLayerAndGroupAndSex(layer, organData.Group, sex);
                if (pool.Count == 0)
                    continue;

                // How many markings this layer gets: mandatory layers always have one,
                // decorative ones roll per remaining point, like upstream RandomizeMarkings.
                var count = 1;
                if (!limits.Required && !random.Prob(limits.Weight))
                    count = 0;
                for (var i = count; i < limits.Limit; i++)
                {
                    if (random.Prob(limits.Weight))
                        count++;
                }

                if (count <= 0)
                    continue;

                var chosen = new List<MarkingPrototype>(pool.Values);

                // Veiru: only really short hairstyles, tinted like the base coat instead of human hair colors.
                var coatTintedHair = species.ID == "Veiru" && layer == HumanoidVisualLayers.Hair;
                if (coatTintedHair)
                    chosen.RemoveAll(p => !ShortVeiruHair.Contains(p.ID));

                if (chosen.Count == 0)
                    continue;

                var layerList = new List<Marking>();

                for (var i = 0; i < count && chosen.Count > 0; i++)
                {
                    var proto = PickWeightedMarking(chosen, random);
                    chosen.Remove(proto);

                    if (layer == HumanoidVisualLayers.Hair || layer == HumanoidVisualLayers.FacialHair)
                    {
                        var tint = coatTintedHair ? GetRelatedMarkingColor(skinColor, random) : hairColor;
                        layerList.Add(new Marking(proto.ID, Enumerable.Repeat(tint, proto.Sprites.Count).ToList()));
                        continue;
                    }

                    layerList.Add(new Marking(proto.ID, PickMarkingColors(proto, skinColor, eyeColor, layerList, random)));
                }

                if (layerList.Count > 0)
                    layerMarkings[layer] = layerList;
            }

            if (layerMarkings.Count > 0)
                result[organ] = layerMarkings;
        }

        return result;
    }

    /// <summary>
    ///     Colors every sprite channel of a marking. Channels with a coloring type in the prototype
    ///     keep their configured behavior; the rest get colors related to the body color.
    /// </summary>
    private List<Color> PickMarkingColors(MarkingPrototype proto, Color skinColor, Color eyeColor, List<Marking> previous, IRobustRandom random)
    {
        var colors = new List<Color>();

        foreach (var sprite in proto.Sprites)
        {
            var name = sprite switch
            {
                SpriteSpecifier.Rsi rsi => rsi.RsiState,
                SpriteSpecifier.Texture texture => texture.TexturePath.Filename,
                _ => null
            };

            var coloringType = (name == null ||
                proto.Coloring.Layers is not { } layers ||
                !layers.TryGetValue(name, out var layerColoring))
                ? proto.Coloring.Default
                : layerColoring;

            var color = coloringType.Type is not null
                ? coloringType.GetColor(skinColor, eyeColor, previous)
                : GetRelatedMarkingColor(skinColor, random);

            colors.Add(color);
        }

        return colors;
    }

    /// <summary>
    ///     Returns a marking color related to the base body color: same color, a shade close to it,
    ///     or a muted natural secondary tone. Never produces saturated neon values.
    /// </summary>
    private static Color GetRelatedMarkingColor(Color baseColor, IRobustRandom random)
    {
        var roll = random.NextFloat(1f);
        var color = baseColor;

        if (roll < 0.40f)
            return color; // same as body

        var hsv = Color.ToHsv(color);

        if (roll < 0.65f)
        {
            // Brightness variation
            var factor = random.Pick(new[] { 0.80f, 0.88f, 1.12f, 1.20f });
            hsv.Z = Math.Clamp(hsv.Z * factor, 0.10f, 0.92f);
        }
        else if (roll < 0.85f)
        {
            // Saturation variation
            hsv.Y = Math.Clamp(hsv.Y * random.Pick(new[] { 0.70f, 0.85f, 1.25f }), 0.04f, 0.65f);
        }
        else
        {
            // Small hue shift toward a natural secondary tone
            var shift = random.Pick(new[] { -16f, -9f, 9f, 16f });
            hsv.X = (hsv.X + shift / 360f) % 1f;
            if (hsv.X < 0f)
                hsv.X += 1f;
        }

        color = Color.FromHsv(hsv);
        return color;
    }
}
