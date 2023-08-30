# email-collector-plugin

# Introduction 

This is an Outlook [VSTO add-in](https://learn.microsoft.com/en-us/visualstudio/vsto/getting-started-programming-vsto-add-ins?view=vs-2022) developed in C# .NET for extracting **completely anonymous** features from emails in the user's inbox. 
These features can constitute a dataset to train Machine Learning models for email phishing detection, as they are the best features in the literature for this task.


# Requirements 

This tool is a [VSTO add-in](https://learn.microsoft.com/en-us/visualstudio/vsto/getting-started-programming-vsto-add-ins?view=vs-2022). and, therefore, does not work on the new version of Outlook, but just on the classic Windows desktop client (as stated in Microsoft [documentation](https://learn.microsoft.com/en-us/office/dev/add-ins/overview/office-add-ins)). 

Visual Studio is required to build this add-in. Other requirements can be found in the [official Microsoft documentation](https://learn.microsoft.com/en-us/visualstudio/vsto/getting-started-programming-vsto-add-ins?view=vs-2022).


# Build Instruction (in Visual Studio)

To edit the code and build the project, you need to use Microsoft Visual Studio (VS).

Open the project with VS, go on "Build" > "Publish DatasetCollector", and then follow the wizard.

The output folder containing the plugin should be found in the root of the project in the "dataset_collector_outlook-latest-ita" folder.  

# Acknowledgments

The research of Francesco Greco is funded by a PhD fellowship within the framework of the Italian “D.M. n. 352, April 9, 2022”- under the National Recovery and Resilience Plan, Mission 4, Component 2, Investment 3.3 - PhD Project “Investigating XAI techniques to help user defend from phishing attacks”, co-supported by “Auriga S.p.A.” (CUP H91I22000410007).

Thanks to Daniele Palmitesta for his contribution during his bachelor's thesis in **University of Bari "A. Moro"**, Italy.


# Contacts

For any information please contact me at <francesco.greco@uniba.it>!
