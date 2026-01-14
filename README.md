This is Login Function for the Revit_Exporter-27, you need to do some change to the Revit_Exporter-27 to make this work, some changes includes:-

  1. Add TestLogin.dll as project refrence on the Mimar Project


      
  2. Add:
     
     using TestLogin.Services; on top of "mimar.RevitCommands.ExternalCommand"

     And:
     
     
       if (!AuthGuard.EnsureAuthenticated(commandData))
    {
        return Result.Cancelled;
    }

     inside of "public class ExternalCommand : IExternalCommand" function inside of "mimar.RevitCommands.ExternalCommand"





Expected "mimar.RevitCommands.ExternalCommand" should look like this:

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using mimar.Utils;
using frag.View;
using mimar.ViewModel;
using Microsoft.Win32;
using System;
using System.IO;
using TestLogin.Services;

namespace mimar.RevitCommands
{
      [Transaction(TransactionMode.Manual)]
      public class ExternalCommand : IExternalCommand
      {
            // This is the original method for the UI button.
            // It now calls our new, reusable export method.
            public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
            {

            if (!AuthGuard.EnsureAuthenticated(commandData))
            {
                return Result.Cancelled;
            }
            try
                  {
                        UIDocument uidoc = commandData.Application.ActiveUIDocument;
                        Document doc = uidoc.Document;

                // Show Save File Dialog
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.Title = "Select output location";
                dialog.Filter = "Mimar Files (*.mimar)|*.mimar";
                dialog.FileName = "output.mimar";   // default file name



                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    string selectedPath = dialog.FileName;

                    // Export file
                    ExportDocument(doc, selectedPath);

                    TaskDialog.Show("Export", $"Exported successfully:\n{selectedPath}");
                    return Result.Succeeded;
                }
                else
                {
                    TaskDialog.Show("Export", "Export canceled.");
                    return Result.Cancelled;
                }
                //Determine the output path, e.g., in Downloads folder
                                string downloadsPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            "Downloads",
                            "output.frag"
                        );
                string testPath = @"D:\work\frag_tst\public\output.frag";
                        Directory.CreateDirectory(Path.GetDirectoryName(downloadsPath));
                        ExportDocument(doc, downloadsPath);
                        TaskDialog.Show("message", "exported");
                        return Result.Succeeded;
                  }
                  catch (Exception ex)
                  {
                        message = ex.Message;
                        return Result.Failed;
                  }
            }

            /// <summary>
            /// This is our new, reusable method that contains the core export logic.
            /// It can be called from any other application.
            /// </summary>
            /// <param name="doc">The Revit document to export.</param>
            /// <param name="outputFilePath">The path to save the file to.</param>
            public static void ExportDocument(Document doc, string outputFilePath)
            {
                  var test = new test(doc);
                  // here working in revit geometry extract the data from meshes and solids 
                  var (geos, items, attrs) = test.RetrieveCategoriesElements();
                  // here is the next stage of exporting ==> going to flatebuffer exporting (meshes)
                  var processAndCompress = new processAndCompress( geos, items);
                  byte[] compressedData = processAndCompress.ProcessAndCompress(attrs);
                  File.WriteAllBytes(outputFilePath, compressedData);
            }


            
      }
}
