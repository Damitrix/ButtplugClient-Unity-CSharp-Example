using System;
using System.Collections.Generic;
using Buttplug.Client;
using Buttplug.Core.Messages;
using UnityEngine;

/// <summary>
/// Buttplug Client Example for Unity.
/// 
/// --------------------------------------------------------------------
/// ATTENTION: Async is a fairly new feature. For it to work in Unity, you have to go into Edit->Project Settings->Player and set the Scripting Runtime Version to Experimental(.NET 4.6) or higher
/// You can also alter the Script by removing every async and await, but i don't recommend that.
/// --------------------------------------------------------------------
/// 
/// For this to work, you have to get a compiled Buttplug Core and Client Library, plus its dependencies (or compile the Sources yourself) and put them somewhere in your Unity Assets Folder.
/// Unity will find them automatically and add them to your references.
/// This Example will use a Generic vibration to demonstrate Vector3 tracking.
/// Attach this Script to any movable GameObject and move it
/// </summary>
public class ButtplugExample : MonoBehaviour
{
    public string ConnectionUrl = "wss://localhost:12345/buttplug"; //Buttplug Server URL
    public TimeSpan TimeBetweenCommands = new TimeSpan(0, 0, 0, 0, 150); //Do not overcrowd the server/device with commands

    private ButtplugWSClient _bpClient; //Buttplug Client

    private Vector3 _trackedBonePos; //The position we want to translate to the Toy
    private Vector3 _lastTrackedBonePos; //The last position, so we can get the current movement
    private float _deltaTime; //Frametime

    private DateTime lastSent;

    /// <summary>
    /// Initialize the Buttplug Client and Connect
    /// Use async to not block Rendering
    /// </summary>
    async void Start()
    {
        //Initialize your Client and give it any fitting name, we'll call it Unity Client
        _bpClient = new ButtplugWSClient("Unity Client");

        //Try to connect to the Buttplug Server
        await _bpClient.Connect(new Uri(ConnectionUrl));
        //Get all connected Devices
        await _bpClient.RequestDeviceList();
        //Scan for Devices
        await _bpClient.StartScanning();
    }
    
    /// <summary>
    /// Update will be called on every Frame, so every time Unity cycles through everything
    /// </summary>
    void Update()
    {
        //Get Unity generated positional data of your GameObject in the normal Update Method, cause Unity does not support it async
        _trackedBonePos = gameObject.transform.position;
        //Everything, that uses the Unity API will have to be populated here, or in any other Method on the Main Thread (except for Debug.Log() ...hurray!)
        _deltaTime = Time.deltaTime;
        
        //Call async Method, cause from here on we're not going to interact with Unity
        WorkAsync();
    }

    /// <summary>
    /// Everything Buttplug related
    /// </summary>
    async void WorkAsync()
    {
        //If no Connection could be established, end it here.
        if (!_bpClient.IsConnected)
        {
            //You can also try to reconnect here.
            return;
        }

        //Cancel if we did not wait long enough between commands
        if (DateTime.Now - lastSent < TimeBetweenCommands)
        {
            return;
        }

        //Get the Distance, that our object has made
        var distance = Vector3.Distance(_trackedBonePos, _lastTrackedBonePos);
        _lastTrackedBonePos = _trackedBonePos;

        //Try to get the average speed in m/s of the object
        var speed = distance * (1f / _deltaTime);

        //Cap Speed limit
        if (speed > 99)
        {
            //imagine: if this would work in real life
            speed = 99;
        }

        //Iterate through every device present
        foreach (var device in _bpClient.getDevices())
        {
            //Check if the device supports vibratinal commands
            if (device.AllowedMessages.ContainsKey("VibrateCmd"))
            {
                //Create List of Vibrational Commands
                var subCmdList = new List<VibrateCmd.VibrateSubcommand>()
                {
                    //Divide speed by 100 to suit Vibration Range (0 - 1)
                    new VibrateCmd.VibrateSubcommand(0, speed / 100)
                };
                //Send the Command
                await _bpClient.SendDeviceMessage(device, new VibrateCmd(device.Index, subCmdList));
                //Set time of last send
                lastSent = DateTime.Now;
            }
            //Vorze Example
            if (device.AllowedMessages.ContainsKey("VorzeA10CycloneCmd"))
            {
                await _bpClient.SendDeviceMessage(device, new VorzeA10CycloneCmd(device.Index, Convert.ToUInt32(speed), true)); //Convert the Speed to a UInt32, cause that's the right type
            }
        }
    }

    /// <summary>
    /// Send Stop Command to all Devices on Quit
    /// also close Websocket connection on quit, cause Unity does not do it automatically
    /// If you don't include this, the Connection will still exist as long as you are running the Editor. That means it will stall on your next connection attempt (on clicking play).
    /// </summary>
    async void OnApplicationQuit()
    {
        foreach (var device in _bpClient.getDevices())
        {
            await _bpClient.SendDeviceMessage(device, new StopDeviceCmd(device.Index));
        }
        if (_bpClient.IsConnected)
        {
            await _bpClient.Disconnect();
        }
    }
}
