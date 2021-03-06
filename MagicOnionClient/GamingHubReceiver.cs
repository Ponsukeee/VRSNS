﻿using System.Collections.Generic;
using Grpc.Core;
using MagicOnion.Client;
using UniRx.Async;
using UnityEngine;
using VRSNS.Core;
using VRSNS.VRM;
using VRUtils.Components;

namespace MagicOnionClient
{
public class GamingHubReceiver : IGamingHubReceiver
{
    private readonly Dictionary<int, AvatarSynchronizer> playerSynchronizers = new Dictionary<int, AvatarSynchronizer>();
    private readonly Dictionary<int, VRMAvatar> playerAvatars = new Dictionary<int, VRMAvatar>();
    private readonly Dictionary<int, VoiceChat> playerVoiceChats = new Dictionary<int, VoiceChat>();
    private readonly Dictionary<int, ObjectSynchronizer> objectSynchronizers = new Dictionary<int, ObjectSynchronizer>();
    private static int PlayerCount;
    public static bool IsSynchronizing => PlayerCount > 1;

    public async UniTask<IGamingHub> ConnectAsync(Channel grpcChannel)
    {
        var hub = StreamingHubClient.Connect<IGamingHub, IGamingHubReceiver>(grpcChannel, this);
        return hub;
    }

    void IGamingHubReceiver.OnJoinRoom(int id, string playerName, int playerCount)
    {
        PlayerCount = playerCount;
        Notification.Notify($"{playerName}が入室しました");
    }

    void IGamingHubReceiver.OnLeaveRoom(int id, string userName, int playerCount)
    {
        PlayerCount = playerCount;

        if (playerAvatars.TryGetValue(id, out var avatar))
        {
            playerSynchronizers.Remove(id);
            playerAvatars.Remove(id);
            GameObject.DestroyImmediate(avatar.Root);
            Notification.Notify($"{userName}が退室しました");
        }
    }

    public void OnDestroyRoom(int id)
    {
        foreach (var key in playerAvatars.Keys)
        {
            if (key == id) continue;
            
            GameObject.DestroyImmediate(playerAvatars[key].Root);
        }
    }

    public async void OnGenerateAvatar(int id, AvatarTransform avatarTransform, byte[] avatarData)
    {
        var head = new GameObject("head");
        var rightHand = new GameObject("rightHand");
        var leftHand = new GameObject("leftHand");
        head.transform.SetPositionAndRotation(avatarTransform.Head.Position, avatarTransform.Head.Rotation);
        rightHand.transform.SetPositionAndRotation(avatarTransform.RightHand.Position, avatarTransform.RightHand.Rotation);
        leftHand.transform.SetPositionAndRotation(avatarTransform.LeftHand.Position, avatarTransform.LeftHand.Rotation);

        var avatar = new VRMAvatar(head, rightHand, leftHand);
        await avatar.GenerateAvatar(avatarData);

        head.transform.SetParent(avatar.Root.transform);
        rightHand.transform.SetParent(avatar.Root.transform);
        leftHand.transform.SetParent(avatar.Root.transform);

        var synchronizer = avatar.Root.AddComponent<AvatarSynchronizer>();
        synchronizer.SetTargets(avatar.Head, avatar.RightHand, avatar.LeftHand);

        var voiceChat = avatar.Root.AddComponent<VoiceChat>();
        
        playerSynchronizers[id] = synchronizer;
        playerAvatars[id] = avatar;
        playerVoiceChats[id] = voiceChat;
    }

    void IGamingHubReceiver.OnInstantiate(int id, string resourceName)
    {
        var obj = (GameObject) Resources.Load(resourceName);
        var synchronizer = obj.AddComponent<ObjectSynchronizer>();
        objectSynchronizers[id] = synchronizer;
        synchronizer.SetTargets(obj);
    }

    void IGamingHubReceiver.OnDestroy(int id)
    {
        if (objectSynchronizers.TryGetValue(id, out var synchronizer))
        {
            GameObject.DestroyImmediate(synchronizer.GameObject);
            objectSynchronizers.Remove(id);
        }
    }

    void IGamingHubReceiver.OnSynchronizeAvatar(int id, AvatarTransform transform)
    {
        if (playerSynchronizers.TryGetValue(id, out var synchronizer))
        {
            synchronizer.TargetTransform = transform;
        }
    }

    void IGamingHubReceiver.OnMoveObject(int id, ObjectTransform transform)
    {
        if (objectSynchronizers.TryGetValue(id, out var synchronizer))
        {
            synchronizer.TargetTransform = transform;
        }
    }

    void IGamingHubReceiver.OnSpeak(int id, int index, float[] segment)
    {
        if (playerVoiceChats.TryGetValue(id, out var voiceChat))
        {
            voiceChat.VoiceStream(index, segment);
        }
    }
}
}