-- Solution
solution "Pulsar4X"
	configurations { "Debug", "Release" }
	location "Pulsar4X"
	
-- Find the required log4net library
function log4netlib()
	if (os.isdir("Pulsar4X/deps/log4net-1.2.11")) then -- Have the log4net binaries
		if (_ACTION ~= "vs2010") then
			-- Assume mono 2.0
			return "Pulsar4X/deps/log4net-1.2.11/bin/mono/2.0/release/log4net.dll"
		else
			-- .net 4.0 Compact Framework
			return "Pulsar4X/deps/log4net-1.2.11/bin/net-cp/4.0/release/log4net.dll"
		end
	end
end
	
	
	-- WinForms Project, main UI project
	project "Pulsar4X.WinForms"
		kind "WindowedApp"
		language "C#"
		location "Pulsar4X/Pulsar4X.WinForms"
		objdir "Pulsar4X/Pulsar4X.WinForms/obj"
		links { -- Add any needed references here
			"Pulsar4X.Lib",
			"System",
			"System.Data",
			"System.Windows.Forms",
			"System.Drawing",
			log4netlib()
			}
		files { "Pulsar4X/Pulsar4X.WinForms/**.cs" }
		
		configuration "Debug"
			targetdir "Pulsar4X/Pulsar4X.WinForms/bin/Debug"
			defines { "DEBUG" }
			flags { "Symbols" }
			
		configuration "Release"
		targetdir "Pulsar4X/Pulsar4X.WinForms/bin/Release"
			flags { "Optimize" }
			
	-- Lib Project, contains game files
	project "Pulsar4X.Lib"
		kind "SharedLib"
		language "C#"
		location "Pulsar4X/Pulsar4X.Lib"
		objdir "Pulsar4X/Pulsar4X.Lib/obj"
		links { -- Add any needed references here
			"System",
			"System.Data",
			"System.Xml",
			log4netlib()
			}
		files { 
			"Pulsar4X/Pulsar4X.Lib/**.cs",
			}
		
		configuration "Debug"
			targetdir "Pulsar4X/Pulsar4X.Lib/bin/Debug"
			defines { "DEBUG" }
			flags { "Symbols" }
			
		configuration "Release"
			targetdir "Pulsar4X/Pulsar4X.Lib/bin/Release"
			flags { "Optimize" }
			
			

			
function defaultaction(osName, actionName)
   if (actionName == nil) then
     _ACTION = _ACTION or osName
   end	   
   if os.is(osName) then
      _ACTION = _ACTION or actionName
   end
end

defaultaction "vs2010"