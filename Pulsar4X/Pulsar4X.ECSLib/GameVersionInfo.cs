﻿using System.Reflection;

namespace Pulsar4X.ECSLib
{
    /// <summary>
    /// Thius struct is used to prepend the gmae/mode version information in json files. This allows the game to
    /// make decisions on weither or not imported data is compatible.
    /// </summary>
    public struct VersionInfo
    {
        /// <summary>
        /// The name of the product, for the main game it is Pulsar4x, mods should use the name of the mod.
        /// </summary>
        public string Name;

        /// <summary>
        /// The version string. For example 1.1 or Major.Minor
        /// The version number should only contain numeric digits with periods seperating the different sections.
        /// </summary>
        public string VersionString;

        // Interger versions of each section of the build number:
        public int MajorVersion;
        public int MinorVersion;
        
        /// <summary>
        /// A comma seperate list of compatible version numbers, numbers in this list will be deem compatible with the current version in VersionString.
        /// For example: "0.8,0.7,0.9,0.10,0.11,1.0". Note that there is no spaces in the string.
        /// </summary>
        public string CompatibileVersions;

        /// <summary>
        /// Returns a VersionInfo struct for the Game.
        /// @todo need a better way of doing this, the compatible versions string is a big problem.
        /// </summary>
        public static VersionInfo PulsarVersionInfo
        {
            get
            {
                VersionInfo gameVersionInfo = new VersionInfo();
                gameVersionInfo.Name = "Pulsar4x";
                AssemblyName assName = Assembly.GetAssembly(typeof(VersionInfo)).GetName();
                gameVersionInfo.VersionString = assName.Version.Major + "." + assName.Version.Minor;
                gameVersionInfo.MajorVersion = assName.Version.Major;
                gameVersionInfo.MinorVersion = assName.Version.Minor;
                gameVersionInfo.CompatibileVersions = gameVersionInfo.VersionString;
                return gameVersionInfo;
            }
        }


        /// <summary>
        /// Checks that this Version Info is compatible with the version info supplied.
        /// </summary>
        public bool IsCompatibleWith(VersionInfo info)
        {
            var versions = info.CompatibileVersions.Split(',');
            foreach (var ver in versions)
            {
                if (ver == VersionString)
                {
                    return true;
                }
            }

            return false;
        }
    }
}