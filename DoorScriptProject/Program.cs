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
    partial class Program : MyGridProgram
    {
        /*
         *  Hangar Managaer V2
         *  
         *  - Auto open/close doors for your hangar when you come in to land
         *  - Will not open if there is a ship already on the landing pad
         *  - 
         *  - Output a list of all hangar statuses to an LCD screen
         *  - Works on small / medium sized hangars
         *          can work with larger hangars if you have sensors with enough range. 
         *  - should work with modded blocks, so long as they inherit from IMyDoor, IMySensorBlock, IMyInteriorLight etc.
         *  
         *  
         *  || SETUP ||
         *  
         *  Needed: 2 Sensors - One that covers the entire hangar area & outside, and the second covering just above the landing pad  
         *                  See workshop submission for diagram example
         *          A programming block - self explanatory
         *  
         *  Optional (but kinda relevant): Doors - HangarDoors, or regular doors
         *  
         *  Optional: Interior lights - To show hangar status
         *            Rotator lights - To show hangar status
         *            Sound Block - to play sound when door is opening/closing 
         *            
         * 
         *  || INSTRUCTIONS ||
         *    
         *  - Group above components into a group, call it something unique:
         *          1 group per managed hangar.
         *          repeat multiple times based on how many hangars you wish to manage.      
         *  
         *  - Turn Programming Block off.
         *  - Import script, click OK.
         *  - Open "Custom Data" 
         *  - Change "# Hangars" value to the amount of hangars you wish to manage, click OK.
         *  - Click "Edit", then "OK".
         *  - Open "Custom Data" again
         *  - Change "Hangar Group X Name" value to the name of your group with your hangar components in.
         *  - Click "Edit", then "OK" again.
         *  - In Programing Block info section under "DETAILS", if it states "Ready To manage X hangars!", turn PB on.
         *  
         *  
         */

        public int numOfHangars;
        //public Color colorEmpty, colorIsEntering, colorLanded, colorIsLeaving;

        private List<string> hangarNames;
        private List<Hangar> hangarList;
        private int currentHangarIndex = 0;
        private List<string> hangarStatuses;

        enum HangarState { HANGAR_EMPTY, SHIP_ENTERING, SHIP_LANDED, SHIP_LEAVING }

        private MyIni _ini = new MyIni();

        private class Hangar
        {
            //Needed
            public IMySensorBlock sensorHangarDoor, sensorLandingPad;
            private List<IMyDoor> hangarDoors;

            //Optional
            private List<IMyInteriorLight> hangarStatusLights;
            private List<IMyReflectorLight> hangarWarningLights;

            public string HangarID;
            public HangarState CurrentState;

            private MyGridProgram p;

            //Constructor
            public Hangar(MyGridProgram parent, string hangarGroup_Name)
            {
                p = parent;

                HangarID = hangarGroup_Name;
                CurrentState = HangarState.HANGAR_EMPTY;

                //Get all blocks in hangarGroup
                IMyBlockGroup _tmpGroup = p.GridTerminalSystem.GetBlockGroupWithName(hangarGroup_Name);
                if (_tmpGroup == null)
                {
                    throw new ArgumentException($"\n\nCould not find hangar group called {hangarGroup_Name}: Check spelling!\n");
                }

                /* Needed blocks. Script will not run without these blocks & script will exit if these are not set. The basic functionality of a hangar door script */

                //Get sensors & allocate them appropriately. To do this, we calculate the volume of the sensor area. 
                //IMO better than gettingName or equivalent, or passing in two more strings for sensornames. 
                List<IMySensorBlock> _tmpSensorGroup = new List<IMySensorBlock>();
                _tmpGroup.GetBlocksOfType(_tmpSensorGroup);

                //Ensure that there are only 2 sensors in the list.
                if (_tmpSensorGroup.Count != 2)
                {
                    throw new ArgumentException($"\n\nIncorrect number of sensors detected in {hangarGroup_Name}. Should have 2 sensors! \nFound {_tmpSensorGroup.Count} Sensor\n");
                }
                else
                {
                    //check to see if sensor 1 is smaller than sensor 2, if true - swap positions, if false - do nout as it's aready ordered
                    if (GetSensorVolume(_tmpSensorGroup[0]) < GetSensorVolume(_tmpSensorGroup[1]))
                    {
                        _tmpSensorGroup.Swap(0, 1);
                    }
                }

                //tmp list is now sorted by sensor volume high to low - meaning the highest volume sensor is in index 0 & should be set to the sensorHangar
                sensorHangarDoor = _tmpSensorGroup[0]; //Larger sensor
                sensorLandingPad = _tmpSensorGroup[1]; //Smaller sensor

                p.Echo($"\n{hangarGroup_Name} details:");

                /* Optional blocks. Script will still run without these blocks */

                //Get doors
                hangarDoors = new List<IMyDoor>();
                _tmpGroup.GetBlocksOfType(hangarDoors);
                p.Echo($"   Door count: {hangarDoors.Count}");

                //Get status lights
                hangarStatusLights = new List<IMyInteriorLight>();
                _tmpGroup.GetBlocksOfType(hangarStatusLights);
                p.Echo($"   Lights count: {hangarStatusLights.Count}");

                //Get warning lights
                hangarWarningLights = new List<IMyReflectorLight>();
                _tmpGroup.GetBlocksOfType(hangarWarningLights);
                p.Echo($"   Rotator lights count: {hangarWarningLights.Count}");

                //Check & set the currentStatus of the hangar on init, then get full status.
                InitialHangarStatusCheck();
                GetCurrentStatus();

            }

            /* ------ Hangar Methods ------ */

            private void InitialHangarStatusCheck()
            {
                /* For checking the status of the hangar on hangar construction as the "proper" status check relies on the current state of the hangar */
                if (sensorHangarDoor.IsActive && !sensorLandingPad.IsActive)
                {
                    CurrentState = HangarState.SHIP_ENTERING; // no way to detect if the ship was entering or leaving on start
                }
                else if (sensorHangarDoor.IsActive && sensorLandingPad.IsActive)
                {
                    CurrentState = HangarState.SHIP_LANDED;
                }
                else
                {
                    CurrentState = HangarState.HANGAR_EMPTY;
                }

            }

            public void GetCurrentStatus()
            {
                /* Check the current state of the hangar & set components accordingly */

                //If ship is outside and no ship in hangar
                if (sensorHangarDoor.IsActive && !sensorLandingPad.IsActive && CurrentState == HangarState.HANGAR_EMPTY)
                {
                    ToggleDoors(true);
                    SetLightsColor(Color.Orange);
                    ToggleWarningLights(true);
                    CurrentState = HangarState.SHIP_ENTERING;
                }

                //If ship is landed
                else if (sensorLandingPad.IsActive && (CurrentState == HangarState.SHIP_ENTERING || CurrentState == HangarState.SHIP_LEAVING))
                {
                    ToggleDoors(false);
                    SetLightsColor(Color.Red);
                    ToggleWarningLights(false);
                    CurrentState = HangarState.SHIP_LANDED;
                }

                //if ship is NOT on pad && state is landed, then the ship is leaving
                else if (!sensorLandingPad.IsActive && CurrentState == HangarState.SHIP_LANDED)
                {
                    ToggleDoors(true);
                    SetLightsColor(Color.Purple);
                    ToggleWarningLights(true);
                    CurrentState = HangarState.SHIP_LEAVING;
                }

                //if no ship is outside and no ship on pad
                else if (!sensorHangarDoor.IsActive && !sensorLandingPad.IsActive)
                {
                    ToggleDoors(false);
                    SetLightsColor(Color.Green);
                    ToggleWarningLights(false);
                    CurrentState = HangarState.HANGAR_EMPTY;
                }

            }

            //Toggle Doors Open/Close
            public void ToggleDoors(bool val)
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

            //Set Color of lights
            public void SetLightsColor(Color col)
            {
                if (hangarStatusLights != null)
                {
                    foreach (IMyInteriorLight i in hangarStatusLights)
                    {
                        i.SetValue("Color", col);
                    }
                }
            }

            //Toggle Rotating Lights
            public void ToggleWarningLights(bool val)
            {
                if (hangarWarningLights != null)
                {
                    foreach (IMyReflectorLight i in hangarWarningLights)
                    {
                        i.Enabled = val;
                    }
                }
            }

            //Convert hangarStatus into a user friendly sentence for use in the hangarStatus list
            public string ReturnHangarStatus()
            {

                string t = $"{HangarID}: ";
                switch (CurrentState)
                {
                    //{ HANGAR_EMPTY, SHIP_ENTERING, SHIP_LANDED, SHIP_LEAVING }
                    case HangarState.HANGAR_EMPTY:
                        t += "Hangar is unoccupied.";
                        break;
                    case HangarState.SHIP_ENTERING:
                        t += $"Ship '{sensorHangarDoor.LastDetectedEntity.Name}' is entering.";
                        break;
                    case HangarState.SHIP_LANDED:
                        t += $"Ship '{sensorHangarDoor.LastDetectedEntity.Name}' has landed.";
                        break;
                    case HangarState.SHIP_LEAVING:
                        t += $"Ship '{sensorHangarDoor.LastDetectedEntity.Name}' is leaving.";
                        break;
                }

                return t;

            }

            //Calculate volume of a sensor's sensing area.
            private float GetSensorVolume(IMySensorBlock sensor)
            {
                float _sensorVolumeX = sensor.LeftExtend + sensor.RightExtend;
                float _sensorVolumeY = sensor.BottomExtend + sensor.TopExtend;
                float _sensorVolumeZ = sensor.BackExtend + sensor.FrontExtend;
                float _vol = (_sensorVolumeX * _sensorVolumeY) * _sensorVolumeZ;
                return _vol;
            }

            /* END OF HANGAR CLASS */

        }



        public Program()
        {
            // The constructor, called only once every session and
            // always before any other method is called. Use it to
            // initialize your script. 
            //     
            // The constructor is optional and can be removed if not
            // needed.
            // 
            // It's recommended to set Runtime.UpdateFrequency 
            // here, which will allow your script to run itself without a 
            // timer block.

            hangarList = new List<Hangar>();
            hangarNames = new List<string>();

            ProcessingIniConfig();

            //add hangars to hangarList
            if (hangarNames != null)
            {
                for (int i = 0; i < hangarNames.Count(); i++)
                {
                    hangarList.Add(new Hangar(this, hangarNames[i]));
                }
            }
            else
            {
                Echo("No Hangars Found!");
                return;
            }

            hangarStatuses = new List<string>();
            if (hangarList != null)
            {
                for (int i = 0; i < hangarList.Count(); i++)
                {
                    hangarStatuses.Add((" ")); //Create empty entry
                }
            }

            Echo($"\nReady to manage {hangarList.Count()} Hangars!");
            Runtime.UpdateFrequency = UpdateFrequency.Update10;

        }

        public void Main(string argument, UpdateType updateSource)
        {
            // The main entry point of the script, invoked every time
            // one of the programmable block's Run actions are invoked,
            // or the script updates itself. The updateSource argument
            // describes where the update came from. Be aware that the
            // updateSource is a  bitfield  and might contain more than 
            // one update type.
            // 
            // The method itself is required, but the arguments above
            // can be removed if not needed.

            if (hangarList.Count() == 0)
            {
                Echo("No 'hangars' in hangarList");
                return;
            }
            else
            {
                //Check to see if the currentIndex is about to overflow from the hangarList size.
                if (currentHangarIndex == hangarList.Count())
                {
                    currentHangarIndex = 0;
                }
            }

            Echo("Checking hangar: " + currentHangarIndex);

            Hangar _han = hangarList[currentHangarIndex];
            _han.GetCurrentStatus();

            //update the current hangar's status text in the statuses list
            hangarStatuses[currentHangarIndex] = _han.ReturnHangarStatus();

            string t = ParseHangarStatuses(_han);
            Echo(t);

            //increment the hangarIndex to check next hangar next time
            currentHangarIndex++;

        }

        //For reading and writing to the custom data field
        void ProcessingIniConfig()
        {
            //Clear to rewrite config()
            _ini.Clear();

            //read from CustomData
            if (_ini.TryParse(Me.CustomData))
            {
                numOfHangars = _ini.Get("Number of Hangar Groups to Manage", "# Hangars").ToInt32(numOfHangars);

                //Get hangars and add them to hangar list.
                for (int i = 0; i < numOfHangars; i++)
                {
                    string configHangarName = _ini.Get("Hangar List", $"Hangar Group {i + 1} Name:").ToString("SET NAME");
                    hangarNames.Add(configHangarName);
                }

            }

            else if (!string.IsNullOrWhiteSpace(Me.CustomData))
            {
                _ini.EndContent = Me.CustomData;
            }

            Me.CustomData = "";
            _ini.Clear();

            //write to CustomData
            _ini.Set("Number of Hangar Groups to Manage", "# Hangars", numOfHangars);

            for (int i = 0; i < numOfHangars; i++)
            {
                _ini.Set("Hangar List", $"Hangar Group {i + 1} Name:", hangarNames[i]);
            }

            Me.CustomData = _ini.ToString();
            string output = _ini.ToString();
            if (output != _ini.ToString())
            {
                Me.CustomData = output;
            }

        }

        //Converts all the hangars into an LCD screen-friendly list, separating each hangar onto a new line
        private string ParseHangarStatuses(Hangar h)
        {
            string t = "";

            for (int i = 0; i < hangarStatuses.Count(); i++)
            {
                t += $"{hangarStatuses[i]}\n";
            }

            return t;
        }

    }
}
