using System.Collections.Generic;
using Hypernex.Networking.Messages.Data;
using Hypernex.Tools;

namespace Hypernex.Configuration.ConfigMeta
{
    public class ConfigUser
    {
        public string Server;
        public string UserId;
        public string Username;
        public string TokenContent;

        public string CurrentAvatar;
        public string HomeWorld;
        public List<string> SavedAvatars = new ();
        public List<string> SavedWorlds = new ();

        public bool UseFacialTracking;
        public Dictionary<string, string> FacialTrackingSettings = new();

        public string Theme;
        public int EmojiType;
        public int AudioCompression; // RAW = 0, OPUS = 1, TODO: enum

        public float VoicesBoost = 0f;
        public float WorldAudioVolume = 1f;
        public bool NoiseSuppression;

        public float VRPlayerHeight;
        public bool UseSnapTurn;
        public float SnapTurnAngle = 45f;
        public float SmoothTurnSpeed = 1f;

        public Dictionary<string, float> UserVolumes = new();

        public float2 DefaultCameraDimensions = new(1920, 1080);

        public void Clone(ConfigUser c)
        {
            Server = c.Server;
            UserId = c.UserId;
            Username = c.Username;
            TokenContent = c.TokenContent;
            CurrentAvatar = c.CurrentAvatar;
            HomeWorld = c.HomeWorld;
            SavedAvatars = c.SavedAvatars;
            SavedWorlds = c.SavedWorlds;
            UseFacialTracking = c.UseFacialTracking;
            FacialTrackingSettings = c.FacialTrackingSettings;
            EmojiType = c.EmojiType;
            UserVolumes = c.UserVolumes;
        }
    }
}