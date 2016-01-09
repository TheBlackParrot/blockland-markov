if(!isObject(MarkovPhraseDatabase)) {
	new ScriptGroup(MarkovPhraseDatabase);
}

if($Pref::Client::Markov::LoadOnStart $= "") {
	$Pref::Client::Markov::LoadOnStart = 1;
	$Pref::Client::Markov::SaveOnQuit = 1;
	$Pref::Client::Markov::MaxMessageSize = 140;
	$Pref::Client::Markov::CorpusDirectory = "config/client/markov/corpus";
	// 0 = learn only, 1 = user only, 2 = all
	$Pref::Client::Markov::AllowChat = 0;
	$Pref::Client::Markov::Blacklist = "Viso";
	// 0 is the first word, 1 is anywhere in the string
	$Pref::Client::Markov::BlacklistMode = 0;
}

function normalizeMarkov(%str) {
	return strLwr(stripChars(%str, "!@#$%^&*()_+-=[]{}\\|;':\",./<>?”“–"));
}

function MarkovPhraseDatabase::phraseExists(%this, %phrase) {
	%obj = "MarkovPhrase" @ strReplace(%phrase, " ", "");
	return isObject(%obj);
}

function MarkovPhraseDatabase::addToDatabase(%this, %string) {
	if(getWordCount(%string) < 2) {
		return;
	}

	echo("added" SPC %string);

	%string = normalizeMarkov(%string);

	for(%i=0;%i<getWordCount(%string);%i++) {
		if(!%i) {
			continue;
		}

		%prevWord = getWord(%string, %i-1);
		%currWord = getWord(%string, %i);
		%nextWord = getWord(%string, %i+1);

		if(%prevWord $= "" || %currWord $= "") {
			continue;
		}

		%phrase = %prevWord @ %currWord;

		if(%this.phraseExists(%phrase)) {
			%obj = "MarkovPhrase" @ strReplace(%phrase, " ", "");
		} else {
			%obj = new ScriptGroup("MarkovPhrase" @ %phrase) {
				class = "MarkovPhrase";
				phrase = %prevWord SPC %currWord;
				count = 0;
				lastModified = getUTC();
			};
			%this.add(%obj);
		}

		if(%nextWord !$= "") {
			%obj.addChoice(%nextWord);
		}
	}
}

function MarkovPhrase::addChoice(%this, %choice) {
	for(%i=0;%i<%this.count;%i++) {
		if(%this.choice[%i] $= %choice) {
			return;
		}
	}

	%this.choice[%this.count] = %choice;
	%this.count++;
	%this.lastModified = getUTC();
}

function MarkovPhrase::getChoice(%this) {
	if(%this.count <= 0) {
		return;
	}
	return %this.choice[getRandom(0, %this.count-1)];
}

function MarkovPhraseDatabase::generate(%this, %potential) {
	if(%this.getCount() <= 0) {
		return;
	}

	while(%str $= "") {
		%currPhrase = MarkovPhraseDatabase.getObject(getRandom(0, %this.getCount()-1));
		%str = %currPhrase.phrase;
		while(%currPhrase.count && strLen(%str) < $Pref::Client::Markov::MaxMessageSize && !%stop) {
			%str = trim(%str SPC getWord(%nextPhrase, 1));

			%nextPhrase = trim(getWord(%currPhrase.phrase, 1) SPC %currPhrase.getChoice());

			if(%this.phraseExists(%nextPhrase) && getWordCount(%nextPhrase) >= 2) {
				%currPhrase = "MarkovPhrase" @ strReplace(%nextPhrase, " ", "");
			} else {
				%stop = 1;
			}
		}
		%str = trim(%str SPC getWord(%nextPhrase, 1));
	}

	return %str;
}

function MarkovPhraseDatabase::exportDatabase(%this) {
	%file = new FileObject();

	%count = 0;

	for(%i=0;%i<%this.getCount();%i++) {
		%phrase = %this.getObject(%i);
		if(%phrase.lastModified < $Client::Markov::LoadedAt) {
			continue;
		}

		%count++;
		
		%file.openForWrite("config/client/markov/corpus/" @ %phrase.phrase);
		
		%file.writeLine(%phrase.phrase);
		%file.writeLine(%phrase.count);
		%file.writeLine(%phrase.lastModified);

		for(%j=0;%j<%phrase.count;%j++) {
			%file.writeLine(%phrase.choice[%j]);
		}

		if(%count % 250 == 0) {
			echo("Saved" SPC %count SPC "modified/new phrases...");
		}

		%file.close();
	}

	%file.delete();

	echo("Saved" SPC %count SPC "modified/new phrases.");
}

function MarkovPhraseDatabase::importDatabase(%this, %folder) {
	%pattern = %folder @ "/*";
	%filename = findFirstFile(%pattern);

	%file = new FileObject();

	while(isFile(%filename)) {
		%file.openForRead(%filename);

		%phrase = %file.readLine();
		%fixed = strReplace(%phrase, " ", "");
		%count = %file.readLine();
		%lastModified = %file.readLine();

		if(%this.phraseExists(%phrase)) {
			%obj = "MarkovPhrase" @ %fixed;
		} else {
			%obj = new ScriptGroup("MarkovPhrase" @ %fixed) {
				class = "MarkovPhrase";
				phrase = %phrase;
				count = 0;
				lastModified = %lastModified;
			};
			%this.add(%obj);

			while(!%file.isEOF()) {
				%obj.addChoice(%file.readLine());

				%imported++;
				if(%imported % 500 == 0) {
					echo("Imported" SPC %imported SPC "phrases...");
				}
			}
		}

		%file.close();

		%filename = findNextFile(%pattern);
	}

	echo("Imported" SPC %imported SPC "phrases.");
}

package MarkovPackage {
	function clientCmdChatMessage(%a,%b,%c,%fmsg,%cp,%name,%cs,%msg) {
		%norm = normalizeMarkov(%msg);

		if(normalizeMarkov(getSubStr(stripMLControlChars(%msg), 0, 1)) !$= "") {
			%blacklist = $Pref::Client::Markov::Blacklist;
			%skip = false;

			for(%i=0;%i<getFieldCount(%blacklist);%i++) {
				%bword = getField(%blacklist, %i);

				if($Pref::Client::Markov::BlacklistMode) {
					if(stripos(%msg, %bword) != -1) {
						%skip = true;
					}
				} else {
					if(getWord(%norm, 0) $= %bword) {
						%skip = true;
					}
				}
			}

			if(!%skip) {
				MarkovPhraseDatabase.addToDatabase(%norm);
			}
		}

		if(getWord(%msg, 0) $= "!markov") {
			switch($Pref::Client::Markov::AllowChat) {
				case 0:
					return parent::clientCmdChatMessage(%a,%b,%c,%fmsg,%cp,%name,%cs,%msg);

				case 1:
					if(stripMLControlChars(%name) !$= $pref::Player::NetName) {
						return parent::clientCmdChatMessage(%a,%b,%c,%fmsg,%cp,%name,%cs,%msg);
					}
			}

			%word = getWord(%msg, 1);
			
			if(%word !$= "") {
				while(%attempts < 300 && stripos(%phrase, %word) == -1) {
					%attempts++;
					%phrase = MarkovPhraseDatabase.generate();
				}
				commandToServer('messageSent', "** " @ %phrase);
			} else {
				commandToServer('messageSent', "** " @ MarkovPhraseDatabase.generate());
			}
		}

		return parent::clientCmdChatMessage(%a,%b,%c,%fmsg,%cp,%name,%cs,%msg);
	}

	function onExit(%a,%b,%c,%d,%e,%f) {
		if($Pref::Client::Markov::SaveOnQuit) {
			MarkovPhraseDatabase.exportDatabase();
		}
		return parent::onExit(%a,%b,%c,%d,%e,%f);
	}
};
activatePackage(MarkovPackage);

function readExample() {
	if(isFile("config/client/markovCorpus.txt")) {
		%file = new FileObject();
		%file.openForRead("config/client/markovCorpus.txt");

		while(!%file.isEOF()) {
			%line = %file.readLine();
			MarkovPhraseDatabase.addToDatabase(%line);
		}

		%file.close();
		%file.delete();
	}
}

if(!$Client::Markov::Loaded) {
	if($Pref::Client::Markov::LoadOnStart) {
		MarkovPhraseDatabase.importDatabase($Pref::Client::Markov::CorpusDirectory);
	}

	schedule(100, 0, setMarkovLoadedAt);
	$Client::Markov::Loaded = 1;
}

function setMarkovLoadedAt() {
	$Client::Markov::LoadedAt = getUTC();
}