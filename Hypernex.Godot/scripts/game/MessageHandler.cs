using System;
using Hypernex.Networking.Messages;
using Hypernex.Tools;
using Nexport;
using Logger = Hypernex.CCK.Logger;

namespace Hypernex.Game
{
    public static class MessageHandler
    {
        public static void HandleMessage(GameInstance gameInstance, MsgMeta msgMeta, MessageChannel channel)
        {
            switch (msgMeta.DataId)
            {
                case "Hypernex.Networking.Messages.PlayerUpdate":
                {
                    PlayerUpdate playerUpdate = (PlayerUpdate) Convert.ChangeType(msgMeta.Data, typeof(PlayerUpdate));
                    PlayerManagement.HandlePlayerUpdate(gameInstance, playerUpdate);
                    break;
                }
                case "Hypernex.Networking.Messages.PlayerVoice":
                {
                    PlayerVoice playerVoice = (PlayerVoice) Convert.ChangeType(msgMeta.Data, typeof(PlayerVoice));
                    try
                    {
                        PlayerManagement.HandlePlayerVoice(gameInstance, playerVoice);
                    } catch(Exception){}
                    break;
                }
                case "Hypernex.Networking.Messages.PlayerObjectUpdate":
                {
                    PlayerObjectUpdate playerObjectUpdate = (PlayerObjectUpdate) Convert.ChangeType(msgMeta.Data, typeof(PlayerObjectUpdate));
                    PlayerManagement.HandlePlayerObjectUpdate(gameInstance, playerObjectUpdate);
                    break;
                }
                case "Hypernex.Networking.Messages.WeightedObjectUpdate":
                {
                    WeightedObjectUpdate weightedObjectUpdate =
                        (WeightedObjectUpdate) Convert.ChangeType(msgMeta.Data, typeof(WeightedObjectUpdate));
                    PlayerManagement.HandleWeightedObjectUpdate(gameInstance, weightedObjectUpdate);
                    break;
                }
                case "Hypernex.Networking.Messages.WorldObjectUpdate":
                {
                    WorldObjectUpdate worldObjectUpdate =
                        (WorldObjectUpdate) Convert.ChangeType(msgMeta.Data, typeof(WorldObjectUpdate));
                    /*
                    Transform targetObject = AnimationUtility.GetObjectFromRoot(worldObjectUpdate.Object.ObjectLocation,
                        gameInstance.loadedScene);
                    if (targetObject != null)
                    {
                        // We only want to sync objects that have a reason to be synced
                        // Otherwise, clients could just say they claim a player (for example) and control them
                        NetworkSync networkSync = targetObject.gameObject.GetComponent<NetworkSync>();
                        if (networkSync != null)
                            networkSync.HandleNetworkUpdate(worldObjectUpdate);
                        else
                            Logger.CurrentLogger.Warn("NetworkSync not found!");
                    }
                    else
                        Logger.CurrentLogger.Warn("No Object found at Path " + worldObjectUpdate.Object.ObjectLocation);
                    */
                    break;
                }
                case "Hypernex.Networking.Messages.RespondAuth":
                {
                    RespondAuth respondAuth = (RespondAuth) Convert.ChangeType(msgMeta.Data, typeof(RespondAuth));
                    GameInstance instance = GameInstance.FocusedInstance;
                    if (instance == null)
                        break;
                    if (instance.gameServerId == respondAuth.GameServerId &&
                        instance.instanceId == respondAuth.InstanceId)
                    {
                        instance.authed = true;
                        // Resync weights in server
                        if (PlayerRoot.Local != null)
                            PlayerRoot.Local.ResetWeights();
                    }
                    break;
                }
                case "Hypernex.Networking.Messages.PlayerMessage":
                {
                    // TODO: Implement chatbox system (this will go in NetPlayer)
                    PlayerMessage playerMessage = (PlayerMessage) Convert.ChangeType(msgMeta.Data, typeof(PlayerMessage));
                    try
                    {
                        PlayerManagement.HandlePlayerMessage(gameInstance, playerMessage);
                    } catch(Exception){}
                    break;
                }
                case "Hypernex.Networking.Messages.NetworkedEvent":
                {
                    NetworkedEvent networkedEvent =
                        (NetworkedEvent) Convert.ChangeType(msgMeta.Data, typeof(NetworkedEvent));
                    // gameInstance.ScriptEvents.OnServerNetworkEvent.Invoke(networkedEvent.EventName, networkedEvent.Data.ToArray());
                    break;
                }
                case "Hypernex.Networking.Messages.ServerConsoleLog":
                {
                    ServerConsoleLog serverConsoleLog =
                        (ServerConsoleLog) Convert.ChangeType(msgMeta.Data, typeof(ServerConsoleLog));
                    // ConsoleTemplate.AddMessage(serverConsoleLog);
                    break;
                }
            }
        }
    }
}
