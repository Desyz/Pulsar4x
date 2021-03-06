﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using NUnit.Framework.Constraints;
using Pulsar4X.ECSLib;
namespace Pulsar4X.SDL2UI
{

    public class CargoListPannelSimple: UpdateWindowState
    {
        StaticDataStore _staticData;
        EntityState _entityState;
        VolumeStorageDB _volStorageDB;
        Dictionary<Guid, TypeStore> _stores = new Dictionary<Guid, TypeStore>();
        
        public CargoListPannelSimple(StaticDataStore staticData, EntityState entity)
        {
            _staticData = staticData;
            _entityState = entity;

            _volStorageDB = entity.Entity.GetDataBlob<VolumeStorageDB>();
            _entityState.Entity.Manager.ManagerSubpulses.SystemDateChangedEvent += ManagerSubpulsesOnSystemDateChangedEvent;
            Update();
        }

        private void ManagerSubpulsesOnSystemDateChangedEvent(DateTime newdate)
        {
            Update();
        }


        public void Update()
        {
            //we do a deep copy clone so as to avoid a thread collision when we loop through.
            var newDict = new Dictionary<Guid, TypeStore>();
            
            ICollection ic = _volStorageDB.TypeStores;
            lock (ic.SyncRoot)
            {
                foreach (var kvp in _volStorageDB.TypeStores)
                {
                    newDict.Add(kvp.Key, kvp.Value.Clone());
                }
            }

            _stores = newDict;
            
        }


        
        public void Display()
        {
            var width = ImGui.GetWindowWidth() * 0.5f;
            
            ImGui.BeginChild(_entityState.Name, new System.Numerics.Vector2(240, 200), true, ImGuiWindowFlags.AlwaysAutoResize);
            foreach (var typeStore in _stores)
            {
                CargoTypeSD stype = _staticData.CargoTypes[typeStore.Key];
                var freeVolume = typeStore.Value.FreeVolume;
                var maxVolume = typeStore.Value.MaxVolume;
                var storedVolume = maxVolume - freeVolume;
                
                string headerText = stype.Name + " " + Stringify.Volume(freeVolume) + " / " + Stringify.Volume(maxVolume) + " free";
                ImGui.PushID(_entityState.Entity.Guid.ToString());
                if(ImGui.CollapsingHeader(headerText, ImGuiTreeNodeFlags.CollapsingHeader ))
                {
                    ImGui.Columns(3);
                    ImGui.Text("Item");
                    ImGui.NextColumn();
                    ImGui.Text("Count");
                    ImGui.NextColumn();
                    ImGui.Text("Total Mass");
                    ImGui.NextColumn();
                    ImGui.Separator();
                    foreach (var cargoType in typeStore.Value.CurrentStoreInUnits)
                    {
                        ICargoable ctype = typeStore.Value.Cargoables[cargoType.Key];
                        var cname = ctype.Name;
                        var volumeStored = cargoType.Value;
                        var volumePerItem = ctype.VolumePerUnit;
                        var massStored = cargoType.Value * ctype.MassPerUnit;
                        var itemsStored = typeStore.Value.CurrentStoreInUnits[ctype.ID];
                        if (ImGui.Selectable(cname))
                        {
                        }

                        ImGui.NextColumn();
                        ImGui.Text(Stringify.Number(itemsStored));
                        ImGui.NextColumn();
                        ImGui.Text(Stringify.Mass(massStored));
                        ImGui.SetTooltip(Stringify.Volume(volumeStored));
                        ImGui.NextColumn();
                    }

                    ImGui.Columns(1);
                }
            }
            
            
            ImGui.EndChild(); 
        }

        public override bool GetActive()
        {
            return true;
        }

        public override void OnGameTickChange(DateTime newDate)
        {
        }

        public override void OnSystemTickChange(DateTime newDate)
        {
            Update();
        }

        public override void OnSelectedSystemChange(StarSystem newStarSys)
        {
            throw new NotImplementedException();
        }
        
        
    }

    public delegate void CargoItemSelectedHandler(CargoListPannelComplex cargoPannel);
    public class CargoListPannelComplex
    {
        StaticDataStore _staticData;
        EntityState _entityState;
        VolumeStorageDB _volStorageDB;
        Dictionary<Guid, TypeStore> _stores = new Dictionary<Guid, TypeStore>();
        Dictionary<ICargoable, int> _cargoToMove = new Dictionary<ICargoable, int>();
        Dictionary<ICargoable, int> _cargoToMoveUI = new Dictionary<ICargoable, int>();
        Dictionary<ICargoable, int> _cargoToMoveOrders = new Dictionary<ICargoable, int>();
        Dictionary<ICargoable, int> _cargoToMoveDatablob = new Dictionary<ICargoable, int>();
        
        //Dictionary<Guid, CargoTypeStoreVM> _cargoResourceStoresDict = new Dictionary<Guid, CargoTypeStoreVM>();
        //public List<CargoTypeStoreVM> CargoResourceStores { get; } = new List<CargoTypeStoreVM>();
        public ICargoable selectedCargo;
        internal Dictionary<Guid,bool> HeadersIsOpenDict { get; set; }

        public CargoListPannelComplex(StaticDataStore staticData, EntityState entity, Dictionary<Guid,bool> headersOpenDict)
        {
            _staticData = staticData;
            _entityState = entity;
            _volStorageDB = entity.Entity.GetDataBlob<VolumeStorageDB>();
            HeadersIsOpenDict = headersOpenDict;
            
            Update();
        }
        

        public event CargoItemSelectedHandler CargoItemSelectedEvent;

        public void Update()
        {
            //we do a deep copy clone so as to avoid a thread collision when we loop through.
            var newDict = new Dictionary<Guid, TypeStore>();
            
            ICollection ic = _volStorageDB.TypeStores;
            lock (ic.SyncRoot)
            {
                foreach (var kvp in _volStorageDB.TypeStores)
                {
                    newDict.Add(kvp.Key, kvp.Value.Clone());
                }
            }
            _stores = newDict;
            
            
            if (_entityState.Entity.HasDataBlob<CargoTransferDB>())
            {
                var itemsToXfer = _entityState.Entity.GetDataBlob<CargoTransferDB>().GetItemsToTransfer();
                var newxferDict = new Dictionary<ICargoable, int>();
                foreach (var tuple in itemsToXfer)
                {
                    newxferDict.Add(tuple.item, tuple.unitCount);
                }
                _cargoToMoveDatablob = newxferDict;
            }

            if (_entityState.Entity.HasDataBlob<OrderableDB>())
            {
                var orders = _entityState.Entity.GetDataBlob<OrderableDB>().GetActionList();
                var newxferDict = new Dictionary<ICargoable, int>(); 
                foreach (var order in orders)
                {
                    if (order is CargoXferOrder)
                    {
                        var xferOrder = (CargoXferOrder)order;
                        foreach (var tuple in xferOrder.ItemICargoablesToTransfer)
                        {
                            if (!newxferDict.ContainsKey(tuple.item))
                                newxferDict.Add(tuple.item, tuple.amount);
                            else
                                newxferDict[tuple.item] += tuple.amount;
                        }
                    }
                }

                _cargoToMoveOrders = newxferDict;
            }
            UpdateTotalMoving();

        }

        internal List<(ICargoable,int)> GetAllToMoveOut()
        {
            List<(ICargoable,int)> listToMove = new List<(ICargoable,int)>();

            foreach (var item in _cargoToMoveUI)
            {
                if(item.Value < 0)
                    listToMove.Add((item.Key, item.Value * -1));
            }
            return listToMove; 
        }

        internal void ClearUINumbers()
        {
            _cargoToMoveUI = new Dictionary<ICargoable, int>();
            Update();
        }

        internal bool CanStore(Guid cargoTypeID)
        {
            return _stores.ContainsKey(cargoTypeID);
            
        }

        internal void AddUICargoIn(ICargoable cargoItem, int itemCount)
        {
            if(!_cargoToMoveUI.ContainsKey(cargoItem))
                _cargoToMoveUI.Add(cargoItem, itemCount);
            else
                _cargoToMoveUI[cargoItem] += itemCount;
            UpdateTotalMoving();

        }

        public void UpdateTotalMoving()
        {
            var newDict = new Dictionary<ICargoable, int>();
            foreach (var kvp in _cargoToMoveDatablob)
            {
                if(!newDict.ContainsKey(kvp.Key))
                    newDict.Add(kvp.Key, kvp.Value);
                else
                    newDict[kvp.Key] += kvp.Value;
            }
            foreach (var kvp in _cargoToMoveOrders)
            {
                if(!newDict.ContainsKey(kvp.Key))
                    newDict.Add(kvp.Key, kvp.Value);
                else
                    newDict[kvp.Key] += kvp.Value;
            }
            foreach (var kvp in _cargoToMoveUI)
            {
                if(!newDict.ContainsKey(kvp.Key))
                    newDict.Add(kvp.Key, kvp.Value);
                else
                    newDict[kvp.Key] += kvp.Value;
            }
            _cargoToMove = newDict;
        }


        internal bool HasCargoInStore(ICargoable cargoItem)
        {
            if (_stores.ContainsKey(cargoItem.CargoTypeID))
            {
                return _stores[cargoItem.CargoTypeID].CurrentStoreInUnits.ContainsKey(cargoItem.ID);
            }
            return false;
        }

        public void Display()
        {
            
            ImGui.BeginChild(_entityState.Name, new System.Numerics.Vector2(260, 200), true);
            ImGui.Text(_entityState.Name);
            ImGui.Text("Transfer Rate: " + _volStorageDB.TransferRateInKgHr);
            ImGui.Text("At DeltaV < " + Stringify.Velocity(_volStorageDB.TransferRangeDv_mps));

            foreach (var typeStore in _stores)
            {
                CargoTypeSD stype = _staticData.CargoTypes[typeStore.Key];
                var freeVolume = typeStore.Value.FreeVolume;
                var maxVolume = typeStore.Value.MaxVolume;
                var storedVolume = maxVolume - freeVolume;
                
                string headerText = stype.Name + " " + Stringify.Volume(freeVolume) + " / " + Stringify.Volume(maxVolume) + " free";
                ImGui.PushID(_entityState.Entity.Guid.ToString());
                if(ImGui.CollapsingHeader(headerText, ImGuiTreeNodeFlags.CollapsingHeader ))
                {
                    ImGui.Columns(3);
                    ImGui.Text("Item");
                    ImGui.NextColumn();
                    ImGui.Text("Count");
                    ImGui.NextColumn();
                    ImGui.Text("Total Mass");
                    ImGui.NextColumn();
                    ImGui.Separator();

                    foreach (var cargoItemKvp in typeStore.Value.CurrentStoreInUnits.ToArray())
                    {
                        ICargoable cargoItem = _stores[typeStore.Key].Cargoables[cargoItemKvp.Key];
                        if (cargoItem == null)
                        {
                            FactionInfoDB factionInfoDB;
                            //factionInfoDB.
                        }

                        var cname = cargoItem.Name;
                        var unitsStored = cargoItemKvp.Value;
                        var volumePerItem = cargoItem.VolumePerUnit;
                        var volumeStored = _volStorageDB.GetVolumeStored(cargoItem);
                        var massStored = _volStorageDB.GetMassStored(cargoItem);
                        
                        bool isSelected = selectedCargo == cargoItem;
                        if (ImGui.Selectable(cname, isSelected))
                        {
                            selectedCargo = cargoItem;
                            CargoItemSelectedEvent.Invoke(this);
                        }

                        ImGui.NextColumn();
                        ImGui.Text(Stringify.Number(unitsStored));
                        
                        
                        if (_cargoToMove.ContainsKey(cargoItem))
                        {
                            var unitsMoving = _cargoToMove[cargoItem];
                            string text = Stringify.Number(unitsMoving);
                            ImGui.SameLine();

                            float blue = 0f;
                            if (_cargoToMoveDatablob.ContainsKey(cargoItem))
                            {
                                if (_cargoToMoveDatablob[cargoItem] != 0)
                                    blue = 0.25f;
                            }
                            if (_cargoToMoveOrders.ContainsKey(cargoItem))
                            {
                                if (_cargoToMoveOrders[cargoItem] != 0)
                                    blue = 0.5f;
                            }
                            if (_cargoToMoveUI.ContainsKey(cargoItem))
                            {
                                if (_cargoToMoveUI[cargoItem] != 0)
                                    blue = 0.75f;
                            }

                            if(unitsMoving > 0)
                                ImGui.TextColored(new Vector4(0.5f, 1, blue, 1), text);
                            else
                                ImGui.TextColored(new Vector4(1f, 0.5f, blue, 1), text);
                        }
                        
                        ImGui.NextColumn();
                        ImGui.Text(Stringify.Mass(massStored));
                        ImGui.NextColumn();
                    }

                    ImGui.Columns(1);
                }
            }
            
            
            ImGui.EndChild();
        }

    }

    public class CargoTransfer : PulsarGuiWindow
    {
        StaticDataStore _staticData;
        EntityState _selectedEntityLeft;
        EntityState _selectedEntityRight;
        
        CargoListPannelComplex _cargoList1;
        CargoListPannelComplex CargoListLeft
        {
            get { return _cargoList1; }
            set { _cargoList1 = value; value.CargoItemSelectedEvent += OnCargoItemSelectedEvent; }
        }
        CargoListPannelComplex _cargoList2;
        CargoListPannelComplex CargoListRight
        {
            get { return _cargoList2; }
            set 
            { 
                _cargoList2 = value;
                if(value != null)
                    value.CargoItemSelectedEvent += OnCargoItemSelectedEvent; 
            }
        }
        CargoListPannelComplex SelectedCargoPannel;
        CargoListPannelComplex UnselectedCargoPannel;
        bool _hasCargoAbilityLeft;
        bool _hasCargoAbilityRight;
        Dictionary<Guid, bool> headersOpenDict = new Dictionary<Guid, bool>();
        
        int _transferRate = 0;
        double _dvDifference_ms;
        double _dvMaxRangeDiff_ms;
        
        private CargoTransfer()
        {
            _flags = ImGuiWindowFlags.AlwaysAutoResize;
            ClickedEntityIsPrimary = false;
            
        }
        
        public static CargoTransfer GetInstance(StaticDataStore staticData, EntityState selectedEntity1)
        {
            
            CargoTransfer instance;
            if (!_uiState.LoadedWindows.ContainsKey(typeof(CargoTransfer)))
            {
                instance = new CargoTransfer
                {
                    _staticData = staticData,
                    _selectedEntityLeft = _uiState.PrimaryEntity
                };
                instance.HardRefresh();
            }
            else
            {
                instance = (CargoTransfer)_uiState.LoadedWindows[typeof(CargoTransfer)];
                if (instance._selectedEntityLeft != _uiState.PrimaryEntity)
                {
                    instance.HardRefresh();
                }
            }

            return instance;
        }

        public override void OnSystemTickChange(DateTime newDate)
        {
            if(IsActive) //lets not update unless the window is actualy being displayed.
            {
                if (_cargoList1 != null)
                    _cargoList1.Update();
                if (_cargoList2 != null)
                    _cargoList2.Update();
            }
        }

        void HardRefresh()
        {
            _selectedEntityLeft = _uiState.PrimaryEntity;
            if(_selectedEntityLeft.Entity.HasDataBlob<VolumeStorageDB>())
            {
                CargoListLeft = new CargoListPannelComplex(_staticData, _selectedEntityLeft, headersOpenDict);
                _hasCargoAbilityLeft = true;
            }
            else
                _hasCargoAbilityLeft = false;
            
            
            if (_uiState.PrimaryEntity != _uiState.LastClickedEntity)
            {
                _selectedEntityRight = _uiState.LastClickedEntity;
                if (_selectedEntityRight != null && _selectedEntityLeft.Entity.HasDataBlob<VolumeStorageDB>())
                {
                    if (!_hasCargoAbilityRight)
                        CargoListRight = new CargoListPannelComplex(_staticData, _selectedEntityRight, headersOpenDict);
                    _hasCargoAbilityRight = true;
                }
                else
                    _hasCargoAbilityRight = false;
            }
        }

        internal void Set2ndCargo(EntityState entity)
        {
            if (_selectedEntityLeft.Entity.HasDataBlob<VolumeStorageDB>())
            {
                _selectedEntityRight = entity;
                if (entity.Entity.HasDataBlob<VolumeStorageDB>())
                {
                    CargoListRight = new CargoListPannelComplex(_staticData, _selectedEntityRight, headersOpenDict);

                    CalcTransferRate();

                    _hasCargoAbilityRight = true;
                }
                else
                {
                    CargoListRight = null;
                    _hasCargoAbilityRight = false;
                    _transferRate = 0; 
                }
            }
        }

        void CalcTransferRate()
        {
            
            double? dvDif;
            OrbitDB leftOrbit;
            //TODO: the logic here has places where it's going to break, needs fixing. 
            //I think I'm checking if it's a colony here?
            //but I'm not checking for NewtonMoveDB or OrbitUpdateOftenDB
            if (!_selectedEntityLeft.Entity.HasDataBlob<OrbitDB>()) 
            {
                dvDif = OrbitMath.MeanOrbitalVelocityInm(_selectedEntityRight.Entity.GetDataBlob<OrbitDB>());
            }
            else
            {
                leftOrbit = _selectedEntityLeft.Entity.GetDataBlob<OrbitDB>();
                dvDif = CargoTransferProcessor.CalcDVDifference_m(_selectedEntityLeft.Entity, _selectedEntityRight.Entity);
            }

            if (dvDif == null)
            {
                _transferRate = 0;
            }
            else
            {
                var cargoDBLeft = _selectedEntityLeft.Entity.GetDataBlob<VolumeStorageDB>();
                var cargoDBRight = _selectedEntityRight.Entity.GetDataBlob<VolumeStorageDB>();
                _dvMaxRangeDiff_ms = Math.Max(cargoDBLeft.TransferRangeDv_mps, cargoDBRight.TransferRangeDv_mps);
                _dvDifference_ms = (double)dvDif;
                _transferRate = CargoTransferProcessor.CalcTransferRate(_dvDifference_ms,
                    cargoDBLeft,
                    cargoDBRight);
            }
        }


        // called when item on transfer screen is clicked
        // ought to update currently selected item
        void OnCargoItemSelectedEvent(CargoListPannelComplex cargoPannel)
        {
            SelectedCargoPannel = cargoPannel;
            if (cargoPannel == CargoListLeft)
                UnselectedCargoPannel = CargoListRight;
            else UnselectedCargoPannel = CargoListLeft;

            if(UnselectedCargoPannel != null)
                UnselectedCargoPannel.selectedCargo = null;
            
        }


        internal override void EntityClicked(EntityState entity, MouseButtons button)
        {
            if(button == MouseButtons.Primary)
            {
                Set2ndCargo(entity); 
            }
        }

        private void MoveItems(int amount)
        {
            var selectedCargoItem = SelectedCargoPannel.selectedCargo;
            SelectedCargoPannel.AddUICargoIn(selectedCargoItem, -amount);
            UnselectedCargoPannel.AddUICargoIn(selectedCargoItem, amount);
        }

        private void ActionXferOrder()
        {

            //create order for items to go to right
            CargoXferOrder.CreateCommand(
                _uiState.Game,
                _uiState.Faction,
                _selectedEntityLeft.Entity,
                _selectedEntityRight.Entity, 
                CargoListLeft.GetAllToMoveOut());

            //create order for items to go to left
            CargoXferOrder.CreateCommand(
                _uiState.Game,
                _uiState.Faction,
                _selectedEntityRight.Entity,
                _selectedEntityLeft.Entity,
                CargoListRight.GetAllToMoveOut());
            
            CargoListLeft.ClearUINumbers();
            CargoListRight.ClearUINumbers();
        }


        internal override void Display()
        {
            if (IsActive)
            {
                if (ImGui.Begin("Cargo", ref IsActive, _flags))
                {
                    if (_hasCargoAbilityLeft)
                    {
                        CargoListLeft.Display();
                        ImGui.SameLine();
                        ImGui.BeginChild("xfer", new System.Numerics.Vector2(100, 200));
                        ImGui.Text("Transfer");

                        if (SelectedCargoPannel != null && SelectedCargoPannel.selectedCargo != null)
                        {
                            if (UnselectedCargoPannel != null && UnselectedCargoPannel.CanStore(SelectedCargoPannel.selectedCargo.CargoTypeID))
                            {
                                if (ImGui.Button("x100"))
                                { MoveItems(100); }
                                ImGui.SameLine();
                                if (ImGui.Button("x10"))
                                { MoveItems(10); }
                                ImGui.SameLine();
                                if (ImGui.Button("x1"))
                                { MoveItems(1); }
                                if (ImGui.Button("Action Order"))
                                { ActionXferOrder(); }
                            }
                            //else
                                //can't transfer due to target unable to store this type
                        }

                        ImGui.EndChild();
                        ImGui.SameLine();
                        if (_hasCargoAbilityRight)
                        {
                            
                            CargoListRight.Display();
                            ImGui.Text("DeltaV Difference: " + Stringify.Velocity(_dvDifference_ms));
                            ImGui.Text("Max DeltaV Difference: " + Stringify.Velocity(_dvMaxRangeDiff_ms));
                            ImGui.Text("Transfer Rate Kg/h: " + _transferRate);
                            
                        }
                        else
                            ImGui.Text("Select Entity For Transfer");
                    }

                }
                ImGui.End();
            }
        }
    }


}

