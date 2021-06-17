# Factorio Mod Tool
 I hate it when you have to restart Factorio with 300+ mods enabled/downloaded and it takes 1337 minutes to complete and when you also have to restart like each second.
 
 This tool should add a process of downloading, uninstalling, enabling and disabling mods outside of Factorio.
 
 This tool comes in two flavours:
 1. A CLI tool, factoriomodtool itself.
 2. A WIP GUI wrapper for the factoriomodtool.

Usage: factoriomodtool [--setup] [-h] [-e MOD_ID] [-d MOD_ID] [--redownload MOD_ID] [-i MOD_ID] [-r MOD_ID]

Options:

 -h, --help Print this screen.
 
 --setup Launch setup tool.
  
 -e, --enable MOD_ID Enable mod with mod id MOD_ID.
		
 -d, --disable MOD_ID	Disable mod with mod id MOD_ID.
  
 --redownload MOD_ID	Redownload mod with mod id MOD_ID.
	
 -i, --install, --download MOD_ID	Download a mod from Factorio mod portal with given mod id or mod portal URI. (won't work if you specify a non mod-portal URI)
			
  Example:
			
 --download Krastorio2
			
 --download https://mods.factorio.com/mod/Krastorio2
			
 -r, --uninstall, --remove MOD_ID	Remove downloaded MOD_ID.
