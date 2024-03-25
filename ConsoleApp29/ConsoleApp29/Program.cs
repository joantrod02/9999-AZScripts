using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using BioSero.GreenButtonGo.Scripting;

namespace GreenButtonGo.Scripting
{
    public class BulkLabeling_WorklistParsing_V1 : BioSero.GreenButtonGo.GBGScript
    {

        public void Run(Dictionary<String, Object> variables, RuntimeInfo runtimeInfo)
        {
            //start by pullling in all the protocols from the VX2
            int NumberOfTotalRacks = 0;
            Directory.CreateDirectory(@"C:\ProgramData\BioSero\TempData");
            string tempFilePathForListLoading = @"C:\ProgramData\BioSero\TempData\TempProtocolList.csv";
            string combinedProtocolsvariables = variables["AllSciPrintVX2Protocols"] as string;
            combinedProtocolsvariables = combinedProtocolsvariables.Trim();
            combinedProtocolsvariables = combinedProtocolsvariables.Remove(0, 9);
            string[] stringArray = combinedProtocolsvariables.Split(',');
            File.WriteAllLines(tempFilePathForListLoading, stringArray);

            string SubmittedWorkListPath = variables["str_BatchWorkList_BulkLabeling"] as string; //This value is gather via a GBG screen and passed here into the script


            string labwareTypeForWorkList = Path.GetFileName(SubmittedWorkListPath);//extract the name of the file from the path, then pull the name out from the next to last portion of the file name.

            string[] fileNameAnalysisArray = labwareTypeForWorkList.Split('_');//split name string using underscore delimiter.
            labwareTypeForWorkList = fileNameAnalysisArray[5];//pull only the 6th position in the array for naming the labware
            //MessageBox.Show("MessageBox.Show(selectedReadLineArray.Length.ToString());");
            //labware type isolated now to feed this information back to GBG. 
            variables["BulkLabeling_WorklistdirectedLabwareType"] = labwareTypeForWorkList;//need to correct this by adding the logic on the GBG side first
            //labware type passed up to GBG where conditionals will pick the correct protocol. Take the work list now and determine how many racks to process after taking all the read lines, eliminating the header row and then loop. 
            string[] AllWorkListReadLines = File.ReadAllLines(SubmittedWorkListPath); //take in all the lines of the work list selected by the user
            string[] selectedReadLineArray = AllWorkListReadLines[0].Split(',');//need to count the amount of items in the first read line to determine if the minimum amount of items has been met.

            List<string> AllReadLines_List = new List<string>();


            if (selectedReadLineArray.Length < 12)//determine if the minimum amount of columns have been met. This was using the Hamilton file. 
            {
                MessageBox.Show("Your file doesnt have the correct formatting, make sure to match example file format correctly.");
                return;
            }
            else
            {

                AllReadLines_List = AllWorkListReadLines.ToList();//convert the array to a list and then get rid of the first entry, the first entry is a header row we dont want to mess with during evaluations.
                AllReadLines_List.RemoveAt(0);
            }
            int numberOfVials = 0;
            int LabwareDivisor = 0;
            string LabwareToWriteToDatatable = "";
            if (labwareTypeForWorkList == "05ml")
            {
                LabwareDivisor = 96;
                LabwareToWriteToDatatable = "500ul LVL Tubes";
                //variables["Selected Scinomix Protocol"] = "DLIMS_Hamilton_0.5mL(with_BC_Scan)";     
            }
            if (labwareTypeForWorkList == "20ml")//double check to make sure this name is correct with convention
            {
                LabwareDivisor = 48;
                LabwareToWriteToDatatable = "2000ul LVL Tubes";
                // variables["Selected Scinomix Protocol"] = "DLIMS_Hamilton_2.0mL(with_BC_Scan)";     
            }
            if (labwareTypeForWorkList == "40ml")//double check to make sure this name is correct with convention
            {
                LabwareDivisor = 24;
                LabwareToWriteToDatatable = "4000ul LVL Tubes";
                // variables["Selected Scinomix Protocol"] = "DLIMS_Hamilton_4.0mL(with_BC_Scan)";     
            }
            List<string> UniqueBarcodesFromWorkList = new List<string>();
            foreach (string selectedLineValue in AllReadLines_List)
            {
                //loop over each readline, this tells us the total tubes to process. We will use the standardized name of the labware from the Hamilton to determine the amount of racks to expect. This total number of racks wont work under constraints around spacing in plates
                string[] arrayOfReadLineValues = selectedLineValue.Split(',');

                int valueOfIndexIfFound = UniqueBarcodesFromWorkList.IndexOf(arrayOfReadLineValues[4]);
                if (valueOfIndexIfFound == -1)
                {
                    //MessageBox.Show("");
                    UniqueBarcodesFromWorkList.Add(arrayOfReadLineValues[4]);
                }
                numberOfVials++;
            }
            variables["SciprintVX2_BulkPrint_TotalNumberOfVials"] = numberOfVials;
            //NumberOfTotalRacks = Convert.ToInt32(Math.Ceiling(Convert.ToDouble(numberOfVials)/Convert.ToDouble(LabwareDivisor))); // round up to account for partial racks.
            NumberOfTotalRacks = UniqueBarcodesFromWorkList.Count();
            variables["SciprintVX2_BulkPrint_TotalNumberOfRacks"] = NumberOfTotalRacks;


            //update the database for the storage table with the zone and labware of the number racks for process to work on. 
            var database = EdgyCode.ApplicationManager.BootStrapper.GetInstance<BioSero.GreenButtonGo.Data.RuntimeDataManager>();//instantiate the database
            var carousel = GetInstrument("Carousel");//get the instrument name from GBG
            int rowIDNumber = 1;
            string ExistingRowBC = "";
            int currentRackToInput = 1;
            for (int i = 1; i <= 36; i++)//loop over the amount of racks you want to place into the datatable, can make the 36 value a variable if you can make it to where you get all the rows of the queried data table
            {


                ExistingRowBC = database.GetValueByRowID("Carousel_Hotels", rowIDNumber.ToString(), "Barcode");

                if (ExistingRowBC == "")//ensure there is nothing in the row first prior to placing something in the row
                {

                    if (currentRackToInput > NumberOfTotalRacks)
                    {
                        break;//exit loop when all racks have been inputted into the datatable
                    }
                    database.UpdateRowByID("Carousel_Hotels", rowIDNumber.ToString(), "Zone", "Zone 1");
                    database.UpdateRowByID("Carousel_Hotels", rowIDNumber.ToString(), "Labware", LabwareToWriteToDatatable);
                    database.UpdateRowByID("Carousel_Hotels", rowIDNumber.ToString(), "Barcode", "Rack_" + currentRackToInput.ToString());
                    currentRackToInput++;

                }
                else
                {
                    if (i >= 36)
                    {
                        //else block is here for when there is a barcode present in the datatable row
                        MessageBox.Show("There is no freespace in storage, please remove racks from storage and input the desired racks manually into the storage table.");
                    }

                    rowIDNumber++;

                }


            }

        }
    }
}