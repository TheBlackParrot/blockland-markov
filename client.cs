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
}

function normalizeMarkov(%str) {
	return strLwr(stripChars(%str, "!@#$%^&*()_+-=[]{}\\|;':\",./<>?”“–"));
}

function MarkovPhraseDatabase::phraseExists(%this, %phrase) {
	%obj = "MarkovPhrase" @ strReplace(%phrase, " ", "");
	return isObject(%obj);
}

function MarkovPhraseDatabase::addToDatabase(%this, %string) {
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
}

function MarkovPhrase::getChoice(%this) {
	if(%this.count <= 0) {
		return;
	}
	return %this.choice[getRandom(0, %this.count-1)];
}

function MarkovPhraseDatabase::generate(%this) {
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

	for(%i=0;%i<%this.getCount();%i++) {
		%phrase = %this.getObject(%i);
		
		%file.openForWrite("config/client/markov/corpus/" @ %phrase.phrase);
		
		%file.writeLine(%phrase.phrase);
		%file.writeLine(%phrase.count);
		for(%j=0;%j<%phrase.count;%j++) {
			%file.writeLine(%phrase.choice[%j]);
		}

		%file.close();
	}

	%file.delete();
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

		if(%this.phraseExists(%phrase)) {
			%obj = "MarkovPhrase" @ %fixed;
		} else {
			%obj = new ScriptGroup("MarkovPhrase" @ %fixed) {
				class = "MarkovPhrase";
				phrase = %phrase;
				count = 0;
			};
			%this.add(%obj);

			while(!%file.isEOF()) {
				%obj.addChoice(%file.readLine());
			}
		}

		%file.close();

		%filename = findNextFile(%pattern);
	}
}

package MarkovPackage {
	function clientCmdChatMessage(%a,%b,%c,%fmsg,%cp,%name,%cs,%msg) {
		%norm = normalizeMarkov(%msg);

		if(normalizeMarkov(getSubStr(stripMLControlChars(%msg), 0, 1)) !$= "") {
			MarkovPhraseDatabase.addToDatabase(%norm);
		}

		if(getWord(%msg, 0) $= "!markov") {
			switch($Pref::Client::Markov::AllowChat) {
				case 1:
					if(stripMLControlChars(%name) $= $pref::Player::NetName || stripMLControlChars(%name) $= $pref::Player::LANName) {
						commandToServer('messageSent', "** " @ MarkovPhraseDatabase.generate(getWord(%msg, 1)));
					}

				case 2:
					commandToServer('messageSent', "** " @ MarkovPhraseDatabase.generate(getWord(%msg, 1)));
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

if($Pref::Client::Markov::LoadOnStart) {
	MarkovPhraseDatabase.importDatabase($Pref::Client::Markov::CorpusDirectory);
}