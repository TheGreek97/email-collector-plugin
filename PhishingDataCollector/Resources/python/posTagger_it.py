import spacy

def GetPOSTags (bodyTxt):
    # Load the Italian language model for spaCy
    nlp = spacy.load("it_core_news_sm")

    # Process the text with spaCy to perform POS tagging
    doc = nlp(bodyTxt)

    pos_tags = []

    # Print each word and its corresponding POS tag
    for token in doc:
        pos_tags.append(token.pos_)#[token.text, token.pos_])
    return pos_tags
