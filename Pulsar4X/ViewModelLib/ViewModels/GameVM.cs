﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Pipes;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Pulsar4X.ECSLib;

namespace Pulsar4X.ViewModel
{
    /// <summary>
    /// This view model maps to the main game class. It provides lists of factions, systems and other high level info.
    /// </summary>
    public class GameVM : IViewModel
    {
        private ObservableCollection<SystemVM> _systems;

        private Entity _playerFaction;
 
        //progress bar in MainWindow should be bound to this
        public double ProgressValue
        {
            get { return _progressValue; }
            set
            {
                _progressValue = value;
                OnPropertyChanged();
            }
        }
        private double _progressValue;

        public string StatusText { get{return _statusText;} set { _statusText = value; OnPropertyChanged(); } }
        private string _statusText;

        internal Entity PlayerFaction { get{return _playerFaction;}
            set
            {
                _playerFaction = value;
                //TODO: factionDB.knownfactions need to be filled with... a blank copy of the actual faction that gets filled as the facion finds out more about it?
                //excepting in the case of GM where the actual faction should be good. 
                _visibleFactions = new List<Guid>();
                foreach (var knownFaction in _playerFaction.GetDataBlob<FactionInfoDB>().KnownFactions)
                {
                    _visibleFactions.Add(knownFaction.Guid);
                }
                _systems.Clear();
                _systemDictionary.Clear();
                foreach (var knownsystem in _playerFaction.GetDataBlob<FactionInfoDB>().KnownSystems)
                {
                    SystemVM systemVM = SystemVM.Create(this, knownsystem);
                    _systems.Add(systemVM);
                    _systemDictionary.Add(systemVM.ID, systemVM);
                }
                ColonyScreens = new List<ColonyScreenVM>();
                Colonys = new DictionaryVM<Guid, string>(DisplayMode.Value);
                foreach (var colonyEntity in _playerFaction.GetDataBlob<FactionInfoDB>().Colonies)
                {
                    ColonyScreens.Add(new ColonyScreenVM(colonyEntity, Game.StaticData));
                    Colonys.Add(colonyEntity.Guid, colonyEntity.GetDataBlob<NameDB>().GetName(_playerFaction));
                }
                

            }
        }

        //factions that this client has full visability of. for GM this will be all factions.
        private List<Guid> _visibleFactions; 

        //faction data. for GM this will be compleate, for normal play this will be factions known to the faction, and the factionVM will only contain data that is known to the faction
        private BindingList<FactionVM> _factions; 

        public ObservableCollection<SystemVM> StarSystems { get { return _systems; } }

        public List<ColonyScreenVM> ColonyScreens { get; set; } //TODO create the VM as a view is requested?

        public DictionaryVM<Guid, string> Colonys { get; set; }

        public ColonyScreenVM ColonyScreen { get; set; }

        private Guid _selectedColonyGuid;
        public Guid SetColonyScreen {
            get { return _selectedColonyGuid; }
            set
            {
                _selectedColonyGuid = value; ColonyScreen = new ColonyScreenVM(Game.GlobalManager.GetEntityByGuid(value), Game.StaticData); }
        }

        private Dictionary<Guid, SystemVM> _systemDictionary;

        public SystemVM GetSystem(Guid bodyGuid)
        {
            Entity bodyEntity;
            Guid rootGuid = new Guid();
            if (_systemDictionary.ContainsKey(bodyGuid))
                rootGuid = bodyGuid;
            
            else if (Game.GlobalManager.FindEntityByGuid(bodyGuid, out bodyEntity))
            {                
                if (bodyEntity.HasDataBlob<OrbitDB>())
                {
                     rootGuid = bodyEntity.GetDataBlob<OrbitDB>().ParentDB.Root.Guid;
                }
            }
            else throw new GuidNotFoundException(bodyGuid);

            if (!_systemDictionary.ContainsKey(rootGuid))
            {
                SystemVM systemVM = SystemVM.Create(this, rootGuid);
                _systems.Add(systemVM);
                _systemDictionary.Add(rootGuid,systemVM);
            }
            return _systemDictionary[rootGuid];
        }


        public async void CreateGame(NewGameOptionsVM options)
        {
            StatusText = "Creating Game...";
            Game newGame = await Task.Run(() => Game.NewGame("Test Game", new DateTime(2050, 1, 1), options.NumberOfSystems, options.SelectedModList.Select(dvi => dvi.Directory).ToList(), new Progress<double>(OnProgressUpdate)));
            Game = newGame;

            Entity gameMaster;
            Game.GlobalManager.FindEntityByGuid(Game.GameMasterFaction, out gameMaster);
            PlayerFaction = gameMaster;
            if (options.CreatePlayerFaction && options.DefaultStart)
            {
                PlayerFaction = DefaultStartFactory.DefaultHumans(newGame, options.FactionName);
            }
            ProgressValue = 0;//reset the progressbar
            StatusText = "Game Created.";
        }

        public async void LoadGame(string pathToFile)
        {
            StatusText = "Loading Game...";
            Game = await Task.Run(() => ECSLib.SerializationManager.ImportGame(pathToFile, new Progress<double>(OnProgressUpdate)));

            Entity gameMaster;
            Game.GlobalManager.FindEntityByGuid(Game.GameMasterFaction, out gameMaster);
            PlayerFaction = gameMaster; //TODO a screen with a faction selection and password. 
            ProgressValue = 0;
            StatusText = "Game Loaded.";
        }

        public async void SaveGame(string pathToFile)
        {
            StatusText = "Saving Game...";
            await Task.Run(() => ECSLib.SerializationManager.ExportGame(Game, pathToFile, new Progress<double>(OnProgressUpdate)));
            //await Task.Run(() => SerializationManager.Save(CurrentGame, pathToFile, true)); // Compressed
            ProgressValue = 0;
            StatusText = "Game Saved";
        }

        public async void AdvanceTime(TimeSpan pulseLength, CancellationToken _pulseCancellationToken)
        {
            var pulseProgress = new Progress<double>(UpdatePulseProgress);

            int secondsPulsed;

            try
            {
                secondsPulsed = await Task.Run(() => Game.AdvanceTime((int)pulseLength.TotalSeconds, _pulseCancellationToken, pulseProgress));
                Refresh();
            }
            catch (Exception exception)
            {
                //DisplayException("executing a pulse", exception);
            }
            //e.Handled = true;
            ProgressValue = 0;
        }


        private void UpdatePulseProgress(double progress)
        {
            // Do some UI stuff with Progress percent
            ProgressValue = progress * 100;
        }

        /// <summary>
        /// OnProgressUpdate eventhandler for the Progress class.
        /// Called from the task thread, this call must be marshalled to the UI thread.
        /// </summary>
        private void OnProgressUpdate(double progress)
        {
            // The Dispatcher contains the UI thread. Make sure we are on the UI thread.
            //if (Thread.CurrentThread != Dispatcher.Thread)
            //{
            //    Dispatcher.BeginInvoke(new ProgressUpdate(OnProgressUpdate), progress);
            //    return;
            //}

            ProgressValue = progress * 100;
        }

        private Game _game;

        internal Game Game
        {
            get
            {
                return _game;
            }
            set
            {
                _game = value;
                OnPropertyChanged();
                
                if (PropertyChanged != null)
                {
                    //forces anything listing for a change in the HasGame property to update. 
                    PropertyChanged(this, new PropertyChangedEventArgs("HasGame")); 
                }
            }
        }

        /// <summary>
        /// returns true if a game has been created, loaded etc. 
        /// </summary>
        public bool HasGame
        {
            get { return Game != null; }
        }


        public GameVM()
        {
            //Game = game;
            _systems = new ObservableCollection<SystemVM>();
            _systemDictionary = new Dictionary<Guid, SystemVM>();
            //PlayerFaction = game.GameMasterFaction; //on creation the player faction can be set to GM I guess... for now anyway.
        }

        #region IViewModel

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public void Refresh(bool partialRefresh = false)
        {
            foreach (var system in _systems)
            {
                system.Refresh();
            }
            foreach (var colonyVM in ColonyScreens)
            {
                colonyVM.Refresh();
            }
        }

        #endregion
    }
}