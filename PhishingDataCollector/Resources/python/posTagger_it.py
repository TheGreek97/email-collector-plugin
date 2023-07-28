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