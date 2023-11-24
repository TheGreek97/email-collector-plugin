"""

This file is part of Dataset-Collector.

Dataset-Collector is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

Dataset-Collector is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Dataset-Collector.  If not, see <http://www.gnu.org/licenses/>. 

"""

import spacy
import sys 
import os

def GetPOSTags (bodyTxt, path="./"): # path will be used as it is (i.e. it will be OS dependant)
    # Load the Italian language model for spaCy
    nlp = spacy.load(os.path.join(path, "it_core_news_sm", "it_core_news_sm-3.6.0"))

    # Process the text with spaCy to perform POS tagging
    doc = nlp(bodyTxt)

    pos_tags = []

    # Print each word and its corresponding POS tag
    for token in doc:
        pos_tags.append(token.pos_)#[token.text, token.pos_])
    return pos_tags

if __name__ == "__main__":
    try: 
        folder_path = sys.argv[1]
        text = sys.argv[2]
        POSTags = GetPOSTags(text, folder_path)
        print (POSTags)
    except IndexError:
        print ("Error: argument required!")