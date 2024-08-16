using System;
using System.Collections.Generic;
using System.Linq;
using Hypernex.CCK;
using HypernexSharp.APIObjects;
using HypernexSharp.Socketing.SocketMessages;
using HypernexSharp.Socketing.SocketResponses;
using HypernexSharp.SocketObjects;

namespace Hypernex.Tools
{
    public static class SocketManager
    {
        private static List<SharedAvatarToken> avatarTokens = new();
        public static List<SharedAvatarToken> SharedAvatarTokens => new(avatarTokens);
        public static Action<SharedAvatarToken> OnAvatarToken { get; set; } = token => { };

        private static Dictionary<string, string> assetTokens = new();
        public static Dictionary<string, string> AssetTokens => new(assetTokens);
        public static Action<string, string> OnAssetToken { get; set; } = (fileId, tokenContent) => { };

        public static Action<InstanceOpened, WorldMeta> OnInstanceOpened { get; set; } =
            (openedInstance, worldMeta) => { };
        public static Action<JoinedInstance, WorldMeta> OnInstanceJoined { get; set; } =
            (joinedInstance, worldMeta) => { };
        public static Action<LeftInstance> OnInstanceLeft { get; set; } = instanceLeft => { };
        public static Action<GotInvite> OnInvite { get; set; } = invite => { };

        public static Dictionary<string, string> DownloadedWorlds = new();

        public static void InitSocket()
        {
            // Assuming APITools IsFullReady
            APITools.UserSocket.OnSocketEvent += response =>
            {
                switch (response.message.ToLower())
                {
                    case "joinedinstance":
                        JoinedInstance joinedInstance = (JoinedInstance) response;
                        APITools.GetWorldMeta(joinedInstance.worldId, meta =>
                        {
                            if(meta != null)
                                QuickInvoke.InvokeActionOnMainThread(OnInstanceJoined, joinedInstance, meta);
                            else
                                LeaveInstance(joinedInstance.gameServerId, joinedInstance.instanceId);
                        });
                        break;
                    case "instanceopened":
                        InstanceOpened instanceOpened = (InstanceOpened) response;
                        APITools.GetWorldMeta(instanceOpened.worldId, meta =>
                        {
                            if(meta != null)
                                QuickInvoke.InvokeActionOnMainThread(OnInstanceOpened, instanceOpened, meta);
                        });
                        break;
                    case "leftinstance":
                        LeftInstance leftInstance = (LeftInstance) response;
                        QuickInvoke.InvokeActionOnMainThread(OnInstanceLeft, leftInstance);
                        break;
                    case "gotinvite":
                        GotInvite gotInvite = (GotInvite) response;
                        QuickInvoke.InvokeActionOnMainThread(new Action(() =>
                        {
                            if (AssetTokens.ContainsKey(gotInvite.worldId))
                                assetTokens.Remove(gotInvite.worldId);
                            assetTokens.Add(gotInvite.worldId, gotInvite.assetToken);
                            QuickInvoke.InvokeActionOnMainThread(OnAssetToken, gotInvite.worldId, gotInvite.assetToken);
                        }));
                        QuickInvoke.InvokeActionOnMainThread(OnInvite, gotInvite);
                        break;
                    case "sharedavatartoken":
                        SharedAvatarToken sharedAvatarToken = (SharedAvatarToken) response;
                        QuickInvoke.InvokeActionOnMainThread(new Action(() =>
                        {
                            if (SharedAvatarTokens.Count(x =>
                                    x.avatarId == sharedAvatarToken.avatarId &&
                                    x.fromUserId == sharedAvatarToken.fromUserId) > 0)
                            {
                                foreach (SharedAvatarToken avatarToken in SharedAvatarTokens)
                                    if (avatarToken.avatarId == sharedAvatarToken.avatarId &&
                                        avatarToken.fromUserId == sharedAvatarToken.fromUserId)
                                        avatarTokens.Remove(avatarToken);
                            }
                            avatarTokens.Add(sharedAvatarToken);
                            OnAvatarToken.Invoke(sharedAvatarToken);
                        }));
                        break;
                    case "updatedinstance":
                        UpdatedInstance updatedInstance = (UpdatedInstance) response;
                        if (GameInstance.FocusedInstance.gameServerId == updatedInstance.instanceMeta.GameServerId &&
                            GameInstance.FocusedInstance.instanceId == updatedInstance.instanceMeta.InstanceId)
                            GameInstance.FocusedInstance.UpdateInstanceMeta(updatedInstance);
                        break;
                    case "failedtojoininstance":
                        QuickInvoke.InvokeActionOnMainThread(new Action(() =>
                            Logger.CurrentLogger.Error("Failed to join an instance!")));
                        break;
                    case "failedtocreatetemporaryinstance":
                        QuickInvoke.InvokeActionOnMainThread(new Action(() =>
                            Logger.CurrentLogger.Error("Could not create instance!")));
                        break;
                    // TODO: Implement other messages
                }
            };
            APITools.UserSocket.OnOpen += () => QuickInvoke.InvokeActionOnMainThread(APITools.OnSocketConnect);
            APITools.UserSocket.Open();
            GameInstance.Initialize();
        }

        private static void ContinueCreateInstance(WorldMeta worldMeta, InstancePublicity instancePublicity,
            InstanceProtocol instanceProtocol, GameServer gameServer, string token)
        {
            Builds targetBuild = null;
            foreach (Builds worldMetaBuild in worldMeta.Builds)
            {
                {
                    targetBuild = worldMetaBuild;
                    break;
                }
            }
            if (targetBuild == null)
            {
                Logger.CurrentLogger.Error("No Build supported for your Platform for world " + worldMeta.Name);
                return;
            }
            string fileURL = $"{APITools.APIObject.Settings.APIURL}file/{worldMeta.OwnerId}/{targetBuild.FileId}";
            if (!string.IsNullOrEmpty(token))
                fileURL += "/" + token;
            APITools.APIObject.GetFileMeta(fileMetaResult =>
            {
                string knownHash = String.Empty;
                if (fileMetaResult.success)
                    knownHash = fileMetaResult.result.FileMeta.Hash;
                DownloadTools.DownloadFile(fileURL, $"{worldMeta.Id}.hnw", o =>
                {
                    if (DownloadedWorlds.ContainsKey(worldMeta.Id))
                        DownloadedWorlds.Remove(worldMeta.Id);
                    DownloadedWorlds.Add(worldMeta.Id, o);
                    if (APITools.IsFullReady)
                        APITools.UserSocket.RequestNewInstance(worldMeta, instancePublicity, instanceProtocol, gameServer);
                }, knownHash, p => Init.Instance.loadingOverlay.Report(fileURL, p), Init.Instance.loadingOverlay.Add(fileURL, worldMeta.Name));
            }, worldMeta.OwnerId, targetBuild.FileId);
        }

        public static void CreateInstance(WorldMeta worldMeta, InstancePublicity instancePublicity = InstancePublicity.Anyone,
            InstanceProtocol instanceProtocol = InstanceProtocol.KCP, GameServer gameServer = null)
        {
            if (worldMeta.Publicity == WorldPublicity.OwnerOnly)
            {
                if (worldMeta.OwnerId == APITools.CurrentUser.Id)
                {
                    // Generate a token
                    APITools.APIObject.AddAssetToken(t =>
                        QuickInvoke.InvokeActionOnMainThread(new Action(() =>
                        {
                            if (!t.success)
                                Logger.CurrentLogger.Error("No permission to join world " + worldMeta.Name);
                            else
                            {
                                if (AssetTokens.ContainsKey(worldMeta.Id))
                                    assetTokens.Remove(worldMeta.Id);
                                assetTokens.Add(worldMeta.Id, t.result.token.content);
                                OnAssetToken.Invoke(worldMeta.Id, t.result.token.content);
                                // Force ClosedRequest because only the Owner can distribute AssetTokens
                                ContinueCreateInstance(worldMeta, InstancePublicity.ClosedRequest, instanceProtocol,
                                    gameServer, t.result.token.content);
                            }
                        })), APITools.CurrentUser, APITools.CurrentToken, worldMeta.Id);
                }
                else
                    Logger.CurrentLogger.Error("No permission to join world " + worldMeta.Name);
            }
            else
                ContinueCreateInstance(worldMeta, instancePublicity, instanceProtocol, gameServer, String.Empty);
        }

        private static void ContinueJoinInstance(SafeInstance instance, WorldMeta worldMeta, string token)
        {
            Builds targetBuild = null;
            foreach (Builds worldMetaBuild in worldMeta.Builds)
            {
                {
                    targetBuild = worldMetaBuild;
                    break;
                }
            }
            if (targetBuild == null)
            {
                Logger.CurrentLogger.Error("No Build supported for your Platform for world " + worldMeta.Name);
                return;
            }
            string fileURL = $"{APITools.APIObject.Settings.APIURL}file/{worldMeta.OwnerId}/{targetBuild.FileId}";
            if (!string.IsNullOrEmpty(token))
                fileURL += "/" + token;
            APITools.APIObject.GetFileMeta(fileMetaResult =>
            {
                string knownHash = string.Empty;
                if (fileMetaResult.success)
                    knownHash = fileMetaResult.result.FileMeta.Hash;
                DownloadTools.DownloadFile(fileURL, $"{worldMeta.Id}.hnw", o =>
                {
                    if (DownloadedWorlds.ContainsKey(worldMeta.Id))
                        DownloadedWorlds.Remove(worldMeta.Id);
                    DownloadedWorlds.Add(worldMeta.Id, o);
                    if (APITools.IsFullReady)
                        APITools.UserSocket.JoinInstance(instance.GameServerId, instance.InstanceId);
                }, knownHash, p => Init.Instance.loadingOverlay.Report(fileURL, p), Init.Instance.loadingOverlay.Add(fileURL, worldMeta.Name));
            }, worldMeta.OwnerId, targetBuild.FileId);
        }

        public static void JoinInstance(SafeInstance instance)
        {
            APITools.GetWorldMeta(instance.WorldId, worldMeta =>
            {
                if (worldMeta.Publicity == WorldPublicity.OwnerOnly)
                {
                    if (worldMeta.OwnerId == APITools.CurrentUser.Id)
                    {
                        // Generate a token
                        APITools.APIObject.AddAssetToken(t =>
                            QuickInvoke.InvokeActionOnMainThread(new Action(() =>
                            {
                                if(!t.success)
                                    Logger.CurrentLogger.Error("No permission to join world " + worldMeta.Name);
                                else
                                {
                                    if (AssetTokens.ContainsKey(worldMeta.Id))
                                        assetTokens.Remove(worldMeta.Id);
                                    assetTokens.Add(worldMeta.Id, t.result.token.content);
                                    OnAssetToken.Invoke(worldMeta.Id, t.result.token.content);
                                    ContinueJoinInstance(instance, worldMeta, t.result.token.content);
                                }
                            })), APITools.CurrentUser, APITools.CurrentToken, worldMeta.Id);
                    }
                    else
                    {
                        foreach (KeyValuePair<string,string> assetToken in AssetTokens)
                        {
                            if (assetToken.Key == worldMeta.Id)
                            {
                                ContinueJoinInstance(instance, worldMeta, assetToken.Value);
                                return;
                            }
                        }
                        QuickInvoke.InvokeActionOnMainThread(new Action(() =>
                            Logger.CurrentLogger.Error("Cannot join world " + worldMeta.Name +
                                                       " because you don't have permission!")));
                    }
                }
                else
                    ContinueJoinInstance(instance, worldMeta, String.Empty);
            });
        }

        public static void LeaveInstance(string gameServerId, string instanceId)
        {
            if (APITools.IsFullReady)
                APITools.UserSocket.LeaveInstance(gameServerId, instanceId);
        }

        public static void InviteUser(GameInstance instance, User user)
        {
            if (instance.worldMeta.Publicity == WorldPublicity.OwnerOnly)
            {
                if (instance.worldMeta.OwnerId != APITools.CurrentUser.Id)
                    return;
                APITools.APIObject.AddAssetToken(result =>
                {
                    if (result.success && APITools.IsFullReady)
                        APITools.UserSocket.SendInvite(user, instance.gameServerId, instance.instanceId,
                            result.result.token.content);
                }, APITools.CurrentUser, APITools.CurrentToken, instance.worldMeta.Id);
            }
            else
            {
                if (APITools.IsFullReady)
                    APITools.UserSocket.SendInvite(user, instance.gameServerId, instance.instanceId);
            }
        }
    }
}
