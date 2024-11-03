using GDWeave;
using GDWeave.Godot.Variants;
using GDWeave.Godot;
using GDWeave.Modding;
using System.Text.Json.Serialization;
using System;

namespace Kittenji.LetsGoGambling;

public class Config {
    [JsonInclude] public float volume = 20;

    public static float LinearToDecibel(float linear) {
        float dB;
        if (linear != 0) dB = 20.0f * (float)Math.Log10(linear);
        else dB = -144.0f;
        return dB;
    }

    public int GetVolumeDB() {
        return (int)Math.Round(LinearToDecibel(Math.Clamp(volume, 0f, 100f) / 100f));
    }
}

public class Mod : IMod {
#pragma warning disable CS8618
    public static Config Config;
    public static Serilog.ILogger Logger;
#pragma warning restore CS8618

    public Mod(IModInterface modInterface) {
        Logger = modInterface.Logger;
        Config = modInterface.ReadConfig<Config>();

        Logger.Information("Converted volume: " + Config.GetVolumeDB());

        modInterface.RegisterScriptMod(new ScratchTicketPatch());
    }

    public void Dispose() {

    }
}

public class ScratchTicketPatch : IScriptMod {
    private const string Notif = "notif_audio";
    private const string NotifSound = "notif_sound_fx";

    public bool ShouldRun(string path) => path == "res://Scenes/Minigames/ScratchTicket/scratch_ticket.gdc";

    // returns a list of tokens for the new script, with the input being the original script's tokens
    public IEnumerable<Token> Modify(string path, IEnumerable<Token> tokens) {
        // wait for any newline token after any extends token
        var waiterStart = new MultiTokenWaiter([
            t => t is IdentifierToken {Name: "GlobalAudio"},
            t => t.Type is TokenType.Period,
            t => t is IdentifierToken {Name: "_play_sound"},
            t => t.Type is TokenType.ParenthesisOpen,
            t => t is ConstantToken { Value: StringVariant { Value: "ui_open" } },
            t => t.Type is TokenType.ParenthesisClose,
            t => t.Type is TokenType.Newline
        ], allowPartialMatch: true);

        var waiterLost = new MultiTokenWaiter([
            t => t is IdentifierToken {Name: "GlobalAudio"},
            t => t.Type is TokenType.Period,
            t => t is IdentifierToken {Name: "_play_sound"},
            t => t.Type is TokenType.ParenthesisOpen,
            t => t is ConstantToken { Value: StringVariant { Value: "jingle_lose" } },
            t => t.Type is TokenType.ParenthesisClose,
            t => t.Type is TokenType.Newline
        ], allowPartialMatch: true);

        var waiterWon = new MultiTokenWaiter([
            t => t is IdentifierToken {Name: "GlobalAudio"},
            t => t.Type is TokenType.Period,
            t => t is IdentifierToken {Name: "_play_sound"},
            t => t.Type is TokenType.ParenthesisOpen,
            t => t is ConstantToken { Value: StringVariant { Value: "jingle_win" } },
            t => t.Type is TokenType.ParenthesisClose,
            t => t.Type is TokenType.Newline
        ], allowPartialMatch: true);

        bool startDone = false, lostDone = false;

        // https://github.com/danielah05/WebfishingMods/blob/main/EventAlert/MeteorSpawnPatch.cs
        foreach (var token in tokens) {
            // Mod.Logger.Information(token.ToString());

            if (waiterStart.Check(token)) {
                // found our match, return the original newline
                yield return token;

                foreach (Token temp in SetPlay("res://kittenji.mods/lets_go_gambling/gamble_start.ogg")) {
                    yield return temp;
                }

                startDone = true;
            } else if (startDone && waiterLost.Check(token)) {
                foreach (Token temp in SetPlay("res://kittenji.mods/lets_go_gambling/gamble_lose.ogg", 3, "k_gamble_audio_2", "k_gamble_file_2")) {
                    yield return temp;
                }

                yield return token;
                lostDone = true;
            } else if (lostDone && waiterWon.Check(token)) {
                foreach (Token temp in SetPlay("res://kittenji.mods/lets_go_gambling/gamble_win.ogg", 3, "k_gamble_audio_3", "k_gamble_file_3")) {
                    yield return temp;
                }

                yield return token;
            } else {
                // return the original token
                yield return token;
            }
        }
    }

    private IEnumerable<Token> SetPlay(string path, uint indent = 1, string notifVar = "k_gamble_audio", string fileVar = "k_gamble_file") {
        Mod.Logger.Information("INJECTING AUDIO: " + path);
        // var notif = AudioStreamPlayer.new()
        yield return new Token(TokenType.Newline, indent);
        yield return new Token(TokenType.PrVar);
        yield return new IdentifierToken(notifVar);
        yield return new Token(TokenType.OpAssign);
        yield return new IdentifierToken("AudioStreamPlayer");
        yield return new Token(TokenType.Period);
        yield return new IdentifierToken("new");
        yield return new Token(TokenType.ParenthesisOpen);
        yield return new Token(TokenType.ParenthesisClose);
        yield return new Token(TokenType.Newline, indent);
        // var notifsound = load("aaaaaaaaaa")
        yield return new Token(TokenType.PrVar);
        yield return new IdentifierToken(fileVar);
        yield return new Token(TokenType.OpAssign);
        yield return new Token(TokenType.BuiltInFunc, 76);
        yield return new Token(TokenType.ParenthesisOpen);
        yield return new ConstantToken(new StringVariant(path));
        yield return new Token(TokenType.ParenthesisClose);
        yield return new Token(TokenType.Newline, indent);
        // add_child(notif)
        yield return new IdentifierToken("add_child");
        yield return new Token(TokenType.ParenthesisOpen);
        yield return new IdentifierToken(notifVar);
        yield return new Token(TokenType.ParenthesisClose);
        yield return new Token(TokenType.Newline, indent);
        // notif.set_stream(notifsound)
        yield return new IdentifierToken(notifVar);
        yield return new Token(TokenType.Period);
        yield return new IdentifierToken("set_stream");
        yield return new Token(TokenType.ParenthesisOpen);
        yield return new IdentifierToken(fileVar);
        yield return new Token(TokenType.ParenthesisClose);
        yield return new Token(TokenType.Newline, indent);
        // notif.volume_db = -4
        yield return new IdentifierToken(notifVar);
        yield return new Token(TokenType.Period);
        yield return new IdentifierToken("volume_db");
        yield return new Token(TokenType.OpAssign);
        yield return new ConstantToken(new IntVariant(Mod.Config.GetVolumeDB()));
        yield return new Token(TokenType.Newline, indent);
        // notif.pitch_scale = 1
        yield return new IdentifierToken(notifVar);
        yield return new Token(TokenType.Period);
        yield return new IdentifierToken("pitch_scale");
        yield return new Token(TokenType.OpAssign);
        yield return new ConstantToken(new IntVariant(1));
        yield return new Token(TokenType.Newline, indent);
        // notif.bus = "SFX"
        yield return new IdentifierToken(notifVar);
        yield return new Token(TokenType.Period);
        yield return new IdentifierToken("bus");
        yield return new Token(TokenType.OpAssign);
        yield return new ConstantToken(new StringVariant("SFX"));
        yield return new Token(TokenType.Newline, indent);
        // notif.play()
        yield return new IdentifierToken(notifVar);
        yield return new Token(TokenType.Period);
        yield return new IdentifierToken("play");
        yield return new Token(TokenType.ParenthesisOpen);
        yield return new Token(TokenType.ParenthesisClose);
        yield return new Token(TokenType.Newline, indent);
    }
}
