using EmptyKeys.UserInterface.Generated.DataTemplatesContracts_Bindings;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.Game.WorldEnvironment.Modules;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class HangarDoorManager_V1 : MyGridProgram
    {
        /*
         * Hangar Manager V1
         * 
         * - Manages 1 hangar block per PB.
         * - Added for prosperity
         * 
         * || SETUP ||
         * 
         * Group hangar doors, lights, rotatorLights into separate groups
         * foreach string below, add name of block group & add name of the two sensors.
         * 
         */


        //strings
        string sensorHangar_Name = "Sensor_Door_Hangar01";
        string sensorLandingPad_Name = "Sensor_Landing_Hangar01";
        string lightGroup_Name = "LightsHangar_01";
        string hangarDoorGroup_Name = "HangarDoors_01";
        string warningLights_name = "RotatingLights_Hangar01";

        // ------------------------------------------------- //

        //sensors
        IMySensorBlock sensorHangar;
        IMySensorBlock sensorLandingPad;

        List<IMyInteriorLight> lightsHangar;
        List<IMyDoor> hangarDoors;
        List<IMyReflectorLight> warningLights;

        bool isEntering = false;

        public HangarDoorManager_V1()
        {
            // The constructor, called only once every session and always before any other method is called. Use it to initialize your script. 
            //     
            // It's recommended to set RuntimeInfo.UpdateFrequency here, which will allow your script to run itself without a timer block.


            //Find & set relevant items

            //get sensors
            sensorHangar = GridTerminalSystem.GetBlockWithName(sensorHangar_Name) as IMySensorBlock;
            if (sensorHangar == null)
            {
                Echo("sensorHangar not set - check spelling!");
            }
            sensorLandingPad = GridTerminalSystem.GetBlockWithName(sensorLandingPad_Name) as IMySensorBlock;
            if (sensorLandingPad == null)
            {
                Echo("sensorLandingPad not set - check spelling!");
            }

            //get lights
            IMyBlockGroup _groupLights = GridTerminalSystem.GetBlockGroupWithName(lightGroup_Name);
            lightsHangar = new List<IMyInteriorLight>();
            _groupLights.GetBlocksOfType(lightsHangar);
            if (lightsHangar == null)
            {
                Echo("lightsHangar not set - check spelling!");
            }
            else
            {
                Echo("AllLights Count " + lightsHangar.Count());
            }

            //get doors
            IMyBlockGroup groupHangarDoors = GridTerminalSystem.GetBlockGroupWithName(hangarDoorGroup_Name);
            hangarDoors = new List<IMyDoor>();
            groupHangarDoors.GetBlocksOfType(hangarDoors);
            if (hangarDoors == null)
            {
                Echo("hangarDoors not set - check spelling!");
            }
            else
            {
                Echo("HangarDoors Count " + hangarDoors.Count());
            }

            //get warning lights            
            IMyBlockGroup groupWarningLights = GridTerminalSystem.GetBlockGroupWithName(warningLights_name);
            warningLights = new List<IMyReflectorLight>();
            groupWarningLights.GetBlocksOfType(warningLights);
            if (warningLights == null)
            {
                Echo("warningLights not set - check spelling!");
            }
            else
            {
                Echo("warningLights Count " + warningLights.Count());
            }
            
            Runtime.UpdateFrequency = UpdateFrequency.Update100;

        }

        public void Main(string argument, UpdateType updateSource)
        {
            // The main entry point of the script, invoked every time one of the programmable block's Run actions are invoked, 
            // or the script updates itself. The updateSource argument describes where the update came from.
            // 
            // The method itself is required, but the arguments above can be removed if not needed.


            //Check to see which state the doors and sensors are in.

            //if ship is outside and no ship in hangar && ship is entering hangar
            if (sensorHangar.IsActive && !sensorLandingPad.IsActive && !isEntering)
            {
                OpenDoors(true);
                SetLightsColor(Color.Orange);
                ToggleWarningLights(true);
            }

            //if ship is on pad
            if (sensorLandingPad.IsActive)
            {
                OpenDoors(false);
                SetLightsColor(Color.Red);
                ToggleWarningLights(false);
            }

            //if no ship is outside and no ship on pad
            if (!sensorHangar.IsActive && !sensorLandingPad.IsActive)
            {
                OpenDoors(false);
                SetLightsColor(Color.Green);
                ToggleWarningLights(false);
            }
        }

        //Hangar Methods
        void OpenDoors(bool val)
        {
            if (val == true)
            {
                foreach (IMyDoor i in hangarDoors)
                {
                    i.OpenDoor();
                }
            }

            if (val == false)
            {
                foreach (IMyDoor i in hangarDoors)
                {
                    i.CloseDoor();
                }
            }
        }
        void SetLightsColor(Color col)
        {
            foreach (IMyInteriorLight i in lightsHangar)
            {
                i.SetValue("Color", col);
            }
        }                
        void ToggleWarningLights(bool val)
        {
            foreach (IMyReflectorLight i in warningLights)
            {
                i.Enabled = val;
            }
        }
        
    }
}
