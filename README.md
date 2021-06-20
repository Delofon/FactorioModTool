# Factorio Mod Tool
 I hate it when you have to restart Factorio with 300+ mods enabled/downloaded and it takes 1337 minutes to complete and when you also have to restart like each second.
 
 This tool should add a process of downloading, uninstalling, enabling and disabling mods outside of Factorio.
 
 This tool comes in two flavours:
 1. A CLI tool, factoriomodtool itself.
 2. A WIP GUI wrapper for the factoriomodtool.

		Some of the options features may not be present yet.
		Usage: factoriomodtool [--setup] [-h] [-s] [-f] [-c] [-D] [--get-mods] [--crybaby] [--ignore-error ERROR_CODE] [--disable-all] [-e MOD_ID] [-d MOD_ID] [--redownload MOD_ID] [-i MOD_ID] [-r MOD_ID]
		Options:
		 -h, --help	Print this screen.
		
		 --setup	Launch setup tool.
		
		 -s, --silent	Do not print progress into console.
		 --get-mods	Outputs all enabled mods in a string.
		 --ignore-error ERROR_CODE	Ignore error with code ERROR_CODE. This won't skip fatal errors.
		 --crybaby	Every error is fatal.
		
		 -e, --enable MOD_ID	Enable mod with mod id MOD_ID.
		 -d, --disable MOD_ID	Disable mod with mod id MOD_ID.
		 --disable-all	Disable all mods. (beware: disables base mod)
		 --redownload MOD_ID	Redownload mod with mod id MOD_ID.
		 -i, --install, --download MOD_ID	Download a mod from Factorio mod portal
							with given mod id or mod portal URL.
							(won't work if you specify a non mod-portal URL)
		 -r, --uninstall, --remove MOD_ID	Remove downloaded MOD_ID.
		
		 -c, --no-compatibility	Do not test for compatibility of enabled mods.
		 -D, --no-dependency	Do not download dependency mods.
		 -f, --force-checks	Force compatibility and/or dependency checks even if mod list isn't changed.
