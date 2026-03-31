using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VRDiscordOverlay.Discord.Models;

public class RpcFrame
{
    [JsonProperty("cmd")]
    public string Command { get; set; } = "";

    [JsonProperty("evt")]
    public string? Event { get; set; }

    [JsonProperty("nonce")]
    public string? Nonce { get; set; }

    [JsonProperty("args")]
    public JObject? Args { get; set; }

    [JsonProperty("data")]
    public JObject? Data { get; set; }
}

public class RpcVoiceState
{
    [JsonProperty("mute")]
    public bool Mute { get; set; }

    [JsonProperty("deaf")]
    public bool Deaf { get; set; }

    [JsonProperty("self_mute")]
    public bool SelfMute { get; set; }

    [JsonProperty("self_deaf")]
    public bool SelfDeaf { get; set; }

    [JsonProperty("suppress")]
    public bool Suppress { get; set; }
}

public class RpcUser
{
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("username")]
    public string Username { get; set; } = "";

    [JsonProperty("global_name")]
    public string? GlobalName { get; set; }

    [JsonProperty("discriminator")]
    public string? Discriminator { get; set; }

    [JsonProperty("avatar")]
    public string? Avatar { get; set; }

    [JsonProperty("bot")]
    public bool Bot { get; set; }
}

public class RpcVoiceStateData
{
    [JsonProperty("voice_state")]
    public RpcVoiceState VoiceState { get; set; } = new();

    [JsonProperty("user")]
    public RpcUser User { get; set; } = new();

    [JsonProperty("nick")]
    public string? Nick { get; set; }

    [JsonProperty("volume")]
    public int Volume { get; set; } = 100;

    [JsonProperty("mute")]
    public bool Mute { get; set; }
}

public class RpcSpeakingData
{
    [JsonProperty("user_id")]
    public string UserId { get; set; } = "";
}



public class RpcChannelData
{
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("guild_id")]
    public string? GuildId { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("type")]
    public int Type { get; set; }

    [JsonProperty("voice_states")]
    public List<RpcVoiceStateData> VoiceStates { get; set; } = new();
}
