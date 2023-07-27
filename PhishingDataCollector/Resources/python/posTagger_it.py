import spacy
import sys 
import os

def GetPOSTags (bodyTxt):
    # Load the Italian language model for spaCy
    nlp = spacy.load(os.path.join(os.getcwd(), "it_core_news_sm", "it_core_news_sm-3.6.0"))

    # Process the text with spaCy to perform POS tagging
    doc = nlp(bodyTxt)

    pos_tags = []

    # Print each word and its corresponding POS tag
    for token in doc:
        pos_tags.append(token.pos_)#[token.text, token.pos_])
    return pos_tags

if __name__ == "__main__":
    try: 
        POSTags = GetPOSTags(sys.argv[1])
        print (POSTags)
    except IndexError:
        print ("Error: argument required!")