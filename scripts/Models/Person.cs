using System.Collections.Generic;
using Newtonsoft.Json;

namespace OregonTrail2026.Models;

/// <summary>
/// Individual party member with health, illness tracking, and role assignment.
/// Converted from RenPy: OT2026_data.rpy class Person.
/// </summary>
public class Person
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("alive")]
    public bool Alive { get; set; } = true;

    /// <summary>Health points: 0 = dead, 100 = unconscious threshold, 1000 = perfect.</summary>
    [JsonProperty("health")]
    public int Health { get; set; } = 1000;

    /// <summary>Current illness key from GameConstants.Illnesses (empty if healthy).</summary>
    [JsonProperty("illness")]
    public string Illness { get; set; } = "";

    [JsonProperty("illness_days")]
    public int IllnessDays { get; set; } = 0;

    /// <summary>Severity multiplier 0.0 to 1.0 for damage calculation.</summary>
    [JsonProperty("illness_severity")]
    public float IllnessSeverity { get; set; } = 0.0f;

    [JsonProperty("injury")]
    public string Injury { get; set; } = "";

    /// <summary>Auto-set when health falls below UNCONSCIOUS_THRESHOLD (100).</summary>
    [JsonProperty("unconscious")]
    public bool Unconscious { get; set; } = false;

    /// <summary>Assigned role: driver, hunter, medic, scout, or empty.</summary>
    [JsonProperty("role")]
    public string Role { get; set; } = "";

    [JsonProperty("traits")]
    public List<string> Traits { get; set; } = new();

    [JsonProperty("hooks")]
    public List<string> Hooks { get; set; } = new();

    public Person() { }

    public Person(string name)
    {
        Name = name;
    }

    public bool IsConscious => Alive && Health > GameConstants.UnconsciousThreshold;
}
