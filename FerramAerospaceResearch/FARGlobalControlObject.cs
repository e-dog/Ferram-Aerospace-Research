﻿/*
Ferram Aerospace Research v0.13.3
Copyright 2014, Michael Ferrara, aka Ferram4

    This file is part of Ferram Aerospace Research.

    Ferram Aerospace Research is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Kerbal Joint Reinforcement is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Ferram Aerospace Research.  If not, see <http://www.gnu.org/licenses/>.

    Serious thanks:		a.g., for tons of bugfixes and code-refactorings
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
            			Duxwing, for copy editing the readme
 * 
 * Kerbal Engineer Redux created by Cybutek, Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License
 *      Referenced for starting point for fixing the "editor click-through-GUI" bug
 *
 * Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/55219
 *
 * Toolbar integration powered by blizzy78's Toolbar plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/60863
 */



using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP.IO;
using Toolbar;

namespace ferram4
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class FARGlobalControlEditorObject : UnityEngine.MonoBehaviour
    {
        private int count = 0;
        private FAREditorGUI editorGUI = null;
        private int part_count = -1;
        private IButton FAREditorButton;

        public static bool EditorPartsChanged = false;

        static PluginConfiguration config;

        public void Awake()
        {
            LoadConfigs();
            FAREditorButton = ToolbarManager.Instance.add("ferram4", "FAREditorButton");
            FAREditorButton.TexturePath = "FerramAerospaceResearch/Textures/icon_button";
            FAREditorButton.ToolTip = "FAR Editor Analysis";
            FAREditorButton.OnClick += (e) => FAREditorGUI.minimize = !FAREditorGUI.minimize;
        }


        public void LateUpdate()
        {
            FARAeroUtil.ResetEditorParts();
            FARBaseAerodynamics.GlobalCoLReady = false;

            if (EditorLogic.fetch)
            {
                if (editorGUI == null)
                {
                    editorGUI = new FAREditorGUI();
                    //editorGUI.LoadGUIParameters();
                    editorGUI.RestartCtrlGUI();
                } 
                if (EditorLogic.startPod != null)
                {
                    var editorShip = FARAeroUtil.AllEditorParts;

                    FindPartsWithoutFARModel(editorShip);

                    if (FARAeroUtil.EditorAboutToAttach() && count++ >= 10)
                    {
                        EditorPartsChanged = true;
                        count = 0;
                    }

                    if (part_count != editorShip.Count || EditorPartsChanged)
                    {
                        foreach (Part p in editorShip)
                            foreach (PartModule m in p.Modules)
                                if (m is FARPartModule)
                                    (m as FARPartModule).ForceOnVesselPartsChange();

                        part_count = editorShip.Count;
                        EditorPartsChanged = false;
                    }
                }

            }

        }


        private bool FindPartsWithoutFARModel(List<Part> editorShip)
        {
            bool returnValue = false;
            foreach (Part p in editorShip)
            {
                if(p == null)
                    continue;

                if (p != null && FARAeroUtil.IsNonphysical(p) &&
                    p.physicalSignificance != Part.PhysicalSignificance.NONE)
                {
                    MonoBehaviour.print(p + ": FAR correcting physical significance to fix CoM in editor");
                    p.physicalSignificance = Part.PhysicalSignificance.NONE;
                }

                string title = p.partInfo.title.ToLowerInvariant();

                if (p is StrutConnector || p is FuelLine || p is ControlSurface || p is Winglet || FARPartClassification.ExemptPartFromGettingDragModel(p, title))
                    continue;

                FARPartModule q = p.GetComponent<FARPartModule>();
                if (q != null && !(q is FARControlSys))
                    continue;

                bool updatedModules = false;

                if (FARPartClassification.PartIsCargoBay(p, title))
                {
                    if (!p.Modules.Contains("FARCargoBayModule"))
                    {
                        p.AddModule("FARCargoBayModule");
                        p.Modules["FARCargoBayModule"].OnStart(PartModule.StartState.Editor);
                        FARAeroUtil.AddBasicDragModule(p);
                        p.Modules["FARBasicDragModel"].OnStart(PartModule.StartState.Editor);
                        updatedModules = true;
                    }
                }
                if (!updatedModules)
                {
                    if (FARPartClassification.PartIsPayloadFairing(p, title))
                    {
                        if (!p.Modules.Contains("FARPayloadFairingModule"))
                        {
                            p.AddModule("FARPayloadFairingModule");
                            p.Modules["FARPayloadFairingModule"].OnStart(PartModule.StartState.Editor);
                            FARAeroUtil.AddBasicDragModule(p);
                            p.Modules["FARBasicDragModel"].OnStart(PartModule.StartState.Editor);
                            updatedModules = true;
                        }
                    }

                    if (!updatedModules && !p.Modules.Contains("FARBasicDragModel"))
                    {
                        FARAeroUtil.AddBasicDragModule(p);
                        p.Modules["FARBasicDragModel"].OnStart(PartModule.StartState.Editor);
                        updatedModules = true;
                    }
                }

                returnValue |= updatedModules;

                FARPartModule b = p.GetComponent<FARPartModule>();
                if (b != null)
                    b.VesselPartList = editorShip;             //This prevents every single part in the ship running this due to VesselPartsList not being initialized

               
            }
            return returnValue;
        }

        void OnDestroy()
        {
            SaveConfigs();
            FAREditorButton.Destroy();
        }

        public static void LoadConfigs()
        {
            config = KSP.IO.PluginConfiguration.CreateForType<FAREditorGUI>();
            config.load();
            FARDebugValues.displayForces = Convert.ToBoolean(config.GetValue("displayForces", "false"));
            FARDebugValues.displayCoefficients = Convert.ToBoolean(config.GetValue("displayCoefficients", "false"));
            FARDebugValues.displayShielding = Convert.ToBoolean(config.GetValue("displayShielding", "false"));

            FAREditorGUI.windowPos = config.GetValue("windowPos", new Rect());
            FAREditorGUI.minimize = config.GetValue("EditorGUIBool", true);
            if (FAREditorGUI.windowPos.y < 75)
                FAREditorGUI.windowPos.y = 75;


            FARPartClassification.LoadClassificationTemplates();
            FARAeroUtil.LoadAeroDataFromConfig();
        }

        public static void SaveConfigs()
        {
            config.SetValue("windowPos", FAREditorGUI.windowPos);
            config.SetValue("EditorGUIBool", FAREditorGUI.minimize);
            //print(FARAeroUtil.areaFactor + " " + FARAeroUtil.attachNodeRadiusFactor * 2 + " " + FARAeroUtil.incompressibleRearAttachDrag + " " + FARAeroUtil.sonicRearAdditionalAttachDrag);
            config.save();
        }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class FARGlobalControlFlightObject : UnityEngine.MonoBehaviour
    {
        //private List<Vessel> vesselsWithFARModules = null;
        private IButton FARFlightButton;
        //private Dictionary<Vessel, List<FARPartModule>> vesselFARPartModules = new Dictionary<Vessel, List<FARPartModule>>();
        static PluginConfiguration config;

        public void Awake()
        {
            LoadConfigs();
            FARFlightButton = ToolbarManager.Instance.add("ferram4", "FAREditorButton");
            FARFlightButton.TexturePath = "FerramAerospaceResearch/Textures/icon_button";
            FARFlightButton.ToolTip = "FAR Flight Systems";
            FARFlightButton.OnClick += (e) => FARControlSys.minimize = !FARControlSys.minimize;

            InputLockManager.RemoveControlLock("FAREdLock");
        }

        public void Start()
        {
            GameEvents.onVesselGoOffRails.Add(FindPartsWithoutFARModel);
            GameEvents.onVesselWasModified.Add(UpdateFARPartModules);
            GameEvents.onVesselCreate.Add(UpdateFARPartModules);
        }

        private void UpdateFARPartModules(Vessel v)
        {
            foreach (Part p in v.Parts)
                foreach (PartModule m in p.Modules)
                    if (m is FARPartModule)
                        (m as FARPartModule).ForceOnVesselPartsChange();
/*            List<FARPartModule> FARPartModules;
            if (vesselFARPartModules.TryGetValue(v, out FARPartModules))
            {
                foreach (FARPartModule m in FARPartModules)
                {
                    m.ForceOnVesselPartsChange();
                }
                vesselFARPartModules.Remove(v);

                FARPartModules = new List<FARPartModule>();
                foreach (Part p in v.Parts)
                {
                    foreach (PartModule m in p.Modules)
                        if (m is FARPartModule)
                            FARPartModules.Add(m as FARPartModule);
                }
                vesselFARPartModules.Add(v, FARPartModules);
            }*/
        }

        private void FindPartsWithoutFARModel(Vessel v)
        {
            List<FARPartModule> FARPartModules = new List<FARPartModule>();

            bool returnValue = false;
            foreach (Part p in v.Parts)
            {
                if (p == null)
                    continue;

                string title = p.partInfo.title.ToLowerInvariant();

                if (p is StrutConnector || p is FuelLine || p is ControlSurface || p is Winglet || FARPartClassification.ExemptPartFromGettingDragModel(p, title))
                    continue;

                if (p.Modules.Contains("FARPartModule"))
                {
                    foreach (PartModule m in p.Modules)
                        if (m is FARPartModule)
                            FARPartModules.Add(m as FARPartModule);
                    continue;
                }

                if (p.Modules.Contains("ModuleCommand") && !p.Modules.Contains("FARControlSys"))
                {
                    p.AddModule("FARControlSys");
                    PartModule m = p.Modules["FARControlSys"];
                    m.OnStart(PartModule.StartState.Flying);

                    FARPartModules.Add(m as FARPartModule);
                }

                FARPartModule q = p.GetComponent<FARPartModule>();
                if (q != null && !(q is FARControlSys))
                    continue;

                bool updatedModules = false;

                if (FARPartClassification.PartIsCargoBay(p, title))
                {
                    if (!p.Modules.Contains("FARCargoBayModule"))
                    {
                        p.AddModule("FARCargoBayModule");
                        PartModule m = p.Modules["FARCargoBayModule"];
                        m.OnStart(PartModule.StartState.Flying);
                        FARPartModules.Add(m as FARPartModule);

                        FARAeroUtil.AddBasicDragModule(p);
                        m = p.Modules["FARBasicDragModel"];
                        m.OnStart(PartModule.StartState.Flying);
                        FARPartModules.Add(m as FARPartModule);

                        updatedModules = true;
                    }
                }
                if (!updatedModules)
                {
                    if (FARPartClassification.PartIsPayloadFairing(p, title))
                    {
                        if (!p.Modules.Contains("FARPayloadFairingModule"))
                        {
                            p.AddModule("FARPayloadFairingModule");
                            PartModule m = p.Modules["FARPayloadFairingModule"];
                            m.OnStart(PartModule.StartState.Flying);
                            FARPartModules.Add(m as FARPartModule);

                            FARAeroUtil.AddBasicDragModule(p);
                            m = p.Modules["FARBasicDragModel"];
                            m.OnStart(PartModule.StartState.Flying);
                            FARPartModules.Add(m as FARPartModule);
                            updatedModules = true;
                        }
                    }

                    if (!updatedModules && !p.Modules.Contains("FARBasicDragModel"))
                    {
                        FARAeroUtil.AddBasicDragModule(p);
                        PartModule m = p.Modules["FARBasicDragModel"];
                        m.OnStart(PartModule.StartState.Flying);
                        FARPartModules.Add(m as FARPartModule);

                        updatedModules = true;
                    }
                }

                returnValue |= updatedModules;

                FARPartModule b = p.GetComponent<FARPartModule>();
                if (b != null)
                    b.VesselPartList = v.Parts;             //This prevents every single part in the ship running this due to VesselPartsList not being initialized
            }

            /*if (vesselFARPartModules.ContainsKey(v))
            {
                List<FARPartModule> Modules = vesselFARPartModules[v];
                FARPartModules = FARPartModules.Union(Modules).ToList();
                vesselFARPartModules[v] = FARPartModules;
            }
            else
                vesselFARPartModules.Add(v, FARPartModules);*/
            //return returnValue;
        }


        public void LateUpdate()
        {

            if (FlightGlobals.ready)
            {
/*                if (vesselsWithFARModules == null)
                    vesselsWithFARModules = new List<Vessel>();

                foreach (Vessel v in FlightGlobals.Vessels)
                {
                    if (v.loaded)
                    {
                        if (!vesselsWithFARModules.Contains(v))
                        {
                            vesselsWithFARModules.Add(v);
                            FindPartsWithoutFARModel(v);
                        }
                    }
                    else if (vesselsWithFARModules.Contains(v))
                        vesselsWithFARModules.Remove(v);
                }*/
                FARFlightButton.Visible = FARControlSys.ActiveControlSys && (FARControlSys.ActiveControlSys.vessel == FlightGlobals.ActiveVessel);

            }

            //            else
            //                vesselsWithFARModules = null;
        }

        void OnDestroy()
        {
            SaveConfigs();
            FARFlightButton.Destroy();
            GameEvents.onVesselGoOffRails.Remove(FindPartsWithoutFARModel);
            GameEvents.onVesselWasModified.Remove(UpdateFARPartModules);
            GameEvents.onVesselCreate.Remove(UpdateFARPartModules);
        }

        public static void LoadConfigs()
        {
            config = KSP.IO.PluginConfiguration.CreateForType<FAREditorGUI>();
            config.load();
            FARControlSys.windowPos = config.GetValue("FlightWindowPos", new Rect(100, 100, 150, 100));
            FARControlSys.AutopilotWinPos = config.GetValue("AutopilotWinPos", new Rect());
            FARControlSys.HelpWindowPos = config.GetValue("HelpWindowPos", new Rect());
            FARControlSys.FlightDataPos = config.GetValue("FlightDataPos", new Rect());
            FARControlSys.FlightDataHelpPos = config.GetValue("FlightDataHelpPos", new Rect());
            FARControlSys.AirSpeedPos = config.GetValue("AirSpeedPos", new Rect());
            FARControlSys.AirSpeedHelpPos = config.GetValue("AirSpeedHelpPos", new Rect());
            FARControlSys.minimize = config.GetValue<bool>("FlightGUIBool", false);
            FARControlSys.k_wingleveler_str = config.GetValue("k_wingleveler", "0.05");
            FARControlSys.k_wingleveler = Convert.ToDouble(FARControlSys.k_wingleveler_str);
            FARControlSys.kd_wingleveler_str = config.GetValue("kd_wingleveler", "0.002");
            FARControlSys.kd_wingleveler = Convert.ToDouble(FARControlSys.kd_wingleveler_str);
            FARControlSys.k_yawdamper_str = config.GetValue("k_yawdamper", "0.1");
            FARControlSys.k_yawdamper = Convert.ToDouble(FARControlSys.k_yawdamper_str);
            FARControlSys.k_pitchdamper_str = config.GetValue("k_pitchdamper", "0.25f");
            FARControlSys.k_pitchdamper = Convert.ToDouble(FARControlSys.k_pitchdamper_str);
            FARControlSys.scaleVelocity_str = config.GetValue("scaleVelocity", "150");
            FARControlSys.scaleVelocity = Convert.ToDouble(FARControlSys.scaleVelocity_str);
            FARControlSys.alt_str = config.GetValue("alt", "0");
            FARControlSys.alt = Convert.ToDouble(FARControlSys.alt_str);
            FARControlSys.upperLim_str = config.GetValue("upperLim", "25");
            FARControlSys.upperLim = Convert.ToDouble(FARControlSys.upperLim_str);
            FARControlSys.lowerLim_str = config.GetValue("lowerLim", "-25");
            FARControlSys.lowerLim = Convert.ToDouble(FARControlSys.lowerLim_str);
            FARControlSys.k_limiter_str = config.GetValue("k_limiter", "0.25f");
            FARControlSys.k_limiter = Convert.ToDouble(FARControlSys.k_limiter_str);

            FARControlSys.unitMode = (FARControlSys.SurfaceVelUnit)config.GetValue("unitMode", 0);
            FARControlSys.velMode = (FARControlSys.SurfaceVelMode)config.GetValue("velMode", 0);

            FARDebugValues.displayForces = Convert.ToBoolean(config.GetValue("displayForces", "false"));
            FARDebugValues.displayCoefficients = Convert.ToBoolean(config.GetValue("displayCoefficients", "false"));
            FARDebugValues.displayShielding = Convert.ToBoolean(config.GetValue("displayShielding", "false"));
            FARDebugValues.useSplinesForSupersonicMath = Convert.ToBoolean(config.GetValue("useSplinesForSupersonicMath", "true"));
            FARDebugValues.allowStructuralFailures = Convert.ToBoolean(config.GetValue("allowStructuralFailures", "true"));

            FARAeroStress.LoadStressTemplates();
            FARPartClassification.LoadClassificationTemplates();
            FARAeroUtil.LoadAeroDataFromConfig();
        }

        public static void SaveConfigs()
        {
            config.SetValue("FlightWindowPos", FARControlSys.windowPos);
            config.SetValue("AutopilotWinPos", FARControlSys.AutopilotWinPos);
            config.SetValue("HelpWindowPos", FARControlSys.HelpWindowPos);
            config.SetValue("FlightDataPos", FARControlSys.FlightDataPos);
            config.SetValue("FlightDataHelpPos", FARControlSys.FlightDataHelpPos);
            config.SetValue("AirSpeedPos", FARControlSys.AirSpeedPos);
            config.SetValue("AirSpeedHelpPos", FARControlSys.AirSpeedHelpPos);
            config.SetValue("FlightGUIBool", FARControlSys.minimize);
            config.SetValue("k_wingleveler", (FARControlSys.k_wingleveler).ToString());
            config.SetValue("kd_wingleveler", (FARControlSys.kd_wingleveler).ToString());
            config.SetValue("k_yawdamper", (FARControlSys.k_yawdamper).ToString());
            config.SetValue("k_pitchdamper", (FARControlSys.k_pitchdamper).ToString());
            config.SetValue("scaleVelocity", (FARControlSys.scaleVelocity).ToString());
            config.SetValue("alt", (FARControlSys.alt).ToString());
            config.SetValue("upperLim", (FARControlSys.upperLim).ToString());
            config.SetValue("lowerLim", (FARControlSys.lowerLim).ToString());
            config.SetValue("k_limiter", (FARControlSys.k_limiter).ToString());

            config.SetValue("unitMode", (int)FARControlSys.unitMode);
            config.SetValue("velMode", (int)FARControlSys.velMode); 
            
            config.save();
        }
    }
}
