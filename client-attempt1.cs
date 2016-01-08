// each word gets an object
// potential chains are added as a variable within the object
// - e.g. "word0" = "is", count = 1, etc.
// if a word contains any punctuation, strip it out

if(!isObject(MarkovWordDatabase)) {
	new ScriptGroup(MarkovWordDatabase);
}

function normalizeMarkov(%str) {
	return strLwr(stripChars(%str, "!@#$%^&*()_+-=[]{}\\|;':\",./<>?"));
}
function isMarkovWord(%word) {
	%obj = "MarkovWord" @ %word;
	return isObject(%obj);
}

function MarkovWord::isPossibleChain(%this, %word) {
	%word = normalizeMarkov(%word);

	for(%i=0;%i<%this.count;%i++) {
		if(%this.word[%i] $= %word) {
			return 1;
		}
	}

	return 0;
}
function MarkovWord::addChain(%this, %word) {
	if(!%this.isPossibleChain(%word)) {
		%this.word[%this.count] = %word;
		%this.count++;
	}
}
function MarkovWord::getChain(%this) {
	if(%this.count) {
		return %this.word[getRandom(0, %this.count-1)];
	}
	return;
}

function MarkovWordDatabase::addWord(%this, %word) {
	%word = normalizeMarkov(%word);

	%obj = "MarkovWord" @ %word;
	if(!isObject(%obj)) {
		%obj = new ScriptGroup("MarkovWord" @ %word) {
			class = "MarkovWord";
			word = %word;
			count = 0;
			hits = 0;
		};
		%this.add(%obj);
	} else {
		//if(!%obj.isPossibleChain()) {
		//	%obj.word[%obj.count] = %word;
		//	%obj.count++;
		//}
	}
}

function MarkovWordDatabase::addChains(%this, %string) {
	%string = normalizeMarkov(%string);

	for(%i=0;%i<getWordCount(%string);%i++) {
		%starter = %i ? 0 : 1;
		if(!%starter) {
			%currWord = getWord(%string, %i);
			%prevWord = getWord(%string, %i-1);

			%prevObj = "MarkovWord" @ %prevWord;
			if(!isObject(%prevObj)) {
				%this.addWord(%prevWord);
			}
			%prevObj.addChain(%currWord);
		} else {
			%currWord = getWord(%string, %i);
			%currObj = "MarkovWord" @ %currWord;
			if(!isObject(%currObj)) {
				%this.addWord(%currWord);
			}

			%currObj.canBegin = 1;
		}
	}
}

function MarkovWordDatabase::generate(%this, %startWith) {
	%get = 0;
	if(%startWith !$= "") {
		if(isObject("MarkovWord" @ %startWith)) {
			%starter = ("MarkovWord" @ %startWith);
		} else {
			warn(%startWith SPC "has not been collected");
			%get = 1;
		}
	} else {
		%get = 1;
	}

	if(%get) {
		%starter = %this.getObject(getRandom(0, %this.getCount()-1));

		while(!%starter.canBegin && %attempts < 100) {
			%starter = %this.getObject(getRandom(0, %this.getCount()-1));
			%attempts++;
		}
		if(%attempts >= 100) {
			warn("Attempts to find a starting word in" SPC %this.getName() SPC "exceeded 100.");
		}
	}

	//%str = "";
	//%nextWord = %starter;
	//%nextChain = %starter.getChain();
	//while(%nextChain !$= "" && strLen(%str) < 150 && isObject(%nextWord)) {
	//	if(isObject(%nextWord)) {
	//		%str = trim(%str SPC %nextWord.word);
	//	}
	//	%nextChain = %nextWord.getChain();
	//	%nextWord = "MarkovWord" @ %nextChain;
	//}

	%nextObj = %starter.getName();
	%nextChain = %nextObj.word;

	while(%nextChain !$= "" && strLen(%str) < 150) {
		if(strpos(%nextChain, "MarkovWord") == -1) {
			// assuming this to be a blockland bug, "MarkovWord" occasionally appears in chains
			%str = trim(%str SPC %nextChain);
		}

		if(isObject(%nextObj)) {
			%nextChain = %nextObj.getChain();
			%nextObj = "MarkovWord" @ %nextChain;
		} else {
			%nextChain = "";
		}
	}

	return %str;
}

package MarkovPackage {
	function clientCmdChatMessage(%a,%b,%c,%fmsg,%cp,%name,%cs,%msg) {
		if(normalizeMarkov(getSubStr(%msg, 0, 1)) !$= "") {
			%norm = normalizeMarkov(%msg);
			if(getWordCount(%norm > 1)) {
				MarkovWordDatabase.addChains(%norm);
			}
		}
		if(getWord(%msg, 0) $= "!markov") {
			commandToServer('messageSent', "** " @ MarkovWordDatabase.generate(getWord(%msg, 1)));
		}
		return parent::clientCmdChatMessage(%a,%b,%c,%fmsg,%cp,%name,%cs,%msg);
	}
};
activatePackage(MarkovPackage);