using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Hypernex.Tools;
using Hypernex.Tools.Godot;
using HypernexSharp.APIObjects;

namespace Hypernex.UI
{
    public partial class BigCardTemplate : Control
    {
        public enum CardType
        {
            None,
            User,
            World,
            Instance,
            CurrentInstance,
        }

        [Export(PropertyHint.MultilineText)]
        public string usersLabelFormat = "[center]Users ({0})[/center][right][url]Refresh[/url][/right]";
        [Export]
        public RichTextLabel label;
        [Export]
        public RichTextLabel usersLabel;
        [Export]
        public Container controlsContainer;
        [Export]
        public Container usersContainer;
        [Export]
        public TextureRect background;
        [Export]
        public VideoStreamPlayer videoBackground;
        [Export]
        public PackedScene cardUI;
        [Export]
        public PackedScene buttonUI;

        public CardType type = CardType.None;
        public CardTemplate.CardUserType userType = CardTemplate.CardUserType.Other;
        public User userData = null;
        public WorldMeta worldMeta = null;
        public GameInstance gameInstance = null;
        public SafeInstance safeInstance = null;

        public override void _Ready()
        {
            usersLabel.MetaClicked += OnClick;
        }

        public override void _ExitTree()
        {
            usersLabel.MetaClicked -= OnClick;
        }

        private void OnClick(Variant meta)
        {
            switch (meta.AsString().ToLower())
            {
                default:
                    usersLabel.Text = string.Empty;
                    RefreshUsers();
                    break;
                case "leave":
                    gameInstance?.Dispose();
                    break;
            }
        }

        public void RefreshUsers()
        {
            if (type != CardType.Instance && type != CardType.CurrentInstance)
                return;
            string[] users = Array.Empty<string>();
            if (safeInstance != null)
            {
                users = safeInstance.ConnectedUsers.ToArray();
            }
            else if (gameInstance != null)
            {
                users = gameInstance.ConnectedUsers.Select(x => x.Id).ToArray();
            }
            usersLabel.Text = string.Format(usersLabelFormat, users.Length);
            foreach (var user in users)
            {
                var node = cardUI.Instantiate<CardTemplate>();
                usersContainer.AddChild(node);
                node.SetUserId(user, CardTemplate.CardUserType.Instance);
            }
        }

        public void Reset()
        {
            type = CardType.None;
            userType = CardTemplate.CardUserType.Other;
            userData = null;
            worldMeta = null;
            gameInstance = null;
            safeInstance = null;
            background.Show();
            videoBackground.Stream = null;
            videoBackground.Stop();
            foreach (var child in usersContainer.GetChildren())
                child.QueueFree();
            foreach (var child in controlsContainer.GetChildren())
                child.QueueFree();
            usersLabel.Text = string.Empty;
            Hide();
        }

        public void Refresh()
        {
            switch (type)
            {
                case CardType.User:
                    APITools.APIObject.GetUser(r =>
                    {
                        if (r.success)
                        {
                            APITools.RefreshUser(() =>
                            {
                                if (!IsInstanceValid(label))
                                    return;
                                SetUser(r.result.UserData, userType);
                            });
                        }
                    }, userData.Id, isUserId: true);
                    break;
            }
        }

        public void SetGameInstance(GameInstance instance)
        {
            Reset();
            Name = $"Current Instance ({instance.worldMeta.Name.Replace("[", "[lb]")})";
            type = CardType.CurrentInstance;
            gameInstance = instance;
            label.Text = instance.worldMeta.Name.Replace("[", "[lb]");
            DownloadTools.DownloadBytes(instance.worldMeta.ThumbnailURL, b =>
            {
                if (!IsInstanceValid(background))
                    return;
                Image img = ImageTools.LoadImage(b);
                if (img != null)
                    background.Texture = ImageTexture.CreateFromImage(img);
                else
                {
                    videoBackground.Stream = ImageTools.LoadFFmpeg(b);
                    videoBackground.Play();
                    background.Hide();
                }
            });
            RefreshUsers();
            Show();
        }

        public void SetSafeInstance(SafeInstance instance)
        {
            Reset();
            Name = "Instance (...)";
            APITools.APIObject.GetWorldMeta(r =>
            {
                if (r.success)
                {
                    QuickInvoke.InvokeActionOnMainThread(() =>
                    {
                        if (!IsInstanceValid(label))
                            return;
                        Name = $"Instance ({r.result.Meta.Name.Replace("[", "[lb]")})";
                        type = CardType.Instance;
                        safeInstance = instance;
                        label.Text = r.result.Meta.Name.Replace("[", "[lb]");
                        DownloadTools.DownloadBytes(r.result.Meta.ThumbnailURL, b =>
                        {
                            if (!IsInstanceValid(background))
                                return;
                            Image img = ImageTools.LoadImage(b);
                            if (img != null)
                                background.Texture = ImageTexture.CreateFromImage(img);
                            else
                            {
                                videoBackground.Stream = ImageTools.LoadFFmpeg(b);
                                videoBackground.Play();
                                background.Hide();
                            }
                        });
                        RefreshUsers();
                        Show();
                    });
                }
            }, instance.WorldId);
        }

        public void SetUser(User user, CardTemplate.CardUserType utype)
        {
            Reset();
            Name = "User";
            type = CardType.User;
            userType = utype;
            userData = user;
            label.Text = user.GetUsersName().Replace("[", "[lb]");
            var box1 = controlsContainer.AddVBox();
            box1.AddLabel("User Actions", (ui, v) => { });
            var box2 = box1.AddHBox();
            box2.AddButton("Invite", UIButtonTheme.Primary, ui => SocketManager.InviteUser(GameInstance.FocusedInstance, userData));
            var box3 = box2.AddVBox();
            if (APITools.CurrentUser.Following.Contains(userData.Id))
                box3.AddButton("Unfollow", UIButtonTheme.Secondary, ui =>
                {
                    ui.Text = "...";
                    ui.Disabled = true;
                    APITools.APIObject.UnfollowUser(r => Refresh(), APITools.CurrentUser, APITools.CurrentToken, userData.Id);
                });
            else
                box3.AddButton("Follow", UIButtonTheme.Secondary, ui =>
                {
                    ui.Text = "...";
                    ui.Disabled = true;
                    APITools.APIObject.FollowUser(r => Refresh(), APITools.CurrentUser, APITools.CurrentToken, userData.Id);
                });
            box3.AddButton("Remove Friend", UIButtonTheme.Danger, ui => APITools.APIObject.RemoveFriend(r => Refresh(), APITools.CurrentUser, APITools.CurrentToken, userData.Id));
            box3.AddButton("Block", UIButtonTheme.Danger, ui => APITools.APIObject.BlockUser(r => Refresh(), APITools.CurrentUser, APITools.CurrentToken, userData.Id));
            if (GameInstance.FocusedInstance != null)
            {
                var box4 = box2.AddVBox();
                box4.AddButton("Warn", UIButtonTheme.Warning, ui => GameInstance.FocusedInstance.WarnUser(userData, "TODO"));
                box4.AddButton("Kick", UIButtonTheme.Danger, ui => GameInstance.FocusedInstance.KickUser(userData, "TODO"));
                box4.AddButton("Ban", UIButtonTheme.Danger, ui => GameInstance.FocusedInstance.BanUser(userData, "TODO"));
            }
            DownloadTools.DownloadBytes(user.Bio.PfpURL, b =>
            {
                if (!IsInstanceValid(background))
                    return;
                Image img = ImageTools.LoadImage(b);
                if (img != null)
                    background.Texture = ImageTexture.CreateFromImage(img);
                else
                {
                    videoBackground.Stream = ImageTools.LoadFFmpeg(b);
                    videoBackground.Play();
                    background.Hide();
                }
            });
            Show();
        }

        public void SetWorldMeta(WorldMeta world)
        {
            Reset();
            Name = "World";
            type = CardType.World;
            worldMeta = world;
            label.Text = world.Name.Replace("[", "[lb]");
            DownloadTools.DownloadBytes(world.ThumbnailURL, b =>
            {
                if (!IsInstanceValid(background))
                    return;
                Image img = ImageTools.LoadImage(b);
                if (img != null)
                    background.Texture = ImageTexture.CreateFromImage(img);
                else
                {
                    videoBackground.Stream = ImageTools.LoadFFmpeg(b);
                    videoBackground.Play();
                    background.Hide();
                }
            });
            Show();
        }
    }
}
