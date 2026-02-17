#nullable enable
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OregonTrail2026.Models;

/// <summary>
/// Individual party member with health, illness tracking, and role assignment.
/// Converted from RenPy: OT2026_data.rpy class Person.
/// </summary>
public class Person
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("alive")]
    public bool Alive { get; set; } = true;

    /// <summary>Health points: 0 = dead, 100 = unconscious threshold, 1000 = perfect.</summary>
    [JsonPropertyName("health")]
    public int Health { get; set; } = 1000;

    /// <summary>Current illness key from GameConstants.Illnesses (empty if healthy).</summary>
    [JsonPropertyName("illness")]
    public string Illness { get; set; } = "";

    [JsonPropertyName("illness_days")]
    public int IllnessDays { get; set; } = 0;

    /// <summary>Severity multiplier 0.0 to 1.0 for damage calculation.</summary>
    [JsonPropertyName("illness_severity")]
    public float IllnessSeverity { get; set; } = 0.0f;

    [JsonPropertyName("injury")]
    public string Injury { get; set; } = "";

    /// <summary>Auto-set when health falls below UNCONSCIOUS_THRESHOLD (100).</summary>
    [JsonPropertyName("unconscious")]
    public bool Unconscious { get; set; } = false;

    /// <summary>Assigned role: driver, hunter, medic, scout, or empty.</summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("traits")]
    public List<string> Traits { get; set; } = new();

    [JsonPropertyName("hooks")]
    public List<string> Hooks { get; set; } = new();

    public Person() { }

    public Person(string name)
    {
        Name = name;
    }

    public bool IsConscious => Alive && Health > GameConstants.UnconsciousThreshold;
}
