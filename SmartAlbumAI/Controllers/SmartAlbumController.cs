using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using SmartAlbumAI.Configuration;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace SmartAlbumAI.Controllers
{
    public class SmartAlbumController : Controller
    {
        static CloudBlobClient blobClient;
        const string blobContainerName = "smartalbums";
        static CloudBlobContainer blobContainer;
        private CloudSettings CloudSettings { get; set; }

    
        public SmartAlbumController(IOptions<CloudSettings> options)
        {
            CloudSettings = options.Value;
        }


        public async Task<ActionResult> Index()
        {
            try
            {
                // Retrieve storage account information from connection string
                // How to create a storage connection string - http://msdn.microsoft.com/en-us/library/azure/ee758697.aspx
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudSettings.StorageConnectionString);

                // Create a blob client for interacting with the blob service.
                blobClient = storageAccount.CreateCloudBlobClient();
                blobContainer = blobClient.GetContainerReference(blobContainerName);
                await blobContainer.CreateIfNotExistsAsync();

                // To view the uploaded blob in a browser, you have two options. The first option is to use a Shared Access Signature (SAS) token to delegate  
                // access to the resource. See the documentation links at the top for more information on SAS. The second approach is to set permissions  
                // to allow public access to blobs in this container. Comment the line below to not use this approach and to use SAS. Then you can view the image  
                // using: https://[InsertYourStorageAccountNameHere].blob.core.windows.net/webappstoragedotnet-imagecontainer/FileName 
                await blobContainer.SetPermissionsAsync(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

                // Gets all Cloud Block Blobs in the blobContainerName and passes them to teh view
                List<Uri> allBlobs = new List<Uri>();

                BlobContinuationToken token = null;
                do
                {
                    BlobResultSegment resultSegment =
                        await blobContainer.ListBlobsSegmentedAsync("", true,
                            new BlobListingDetails(), null, token, null, null);
                    token = resultSegment.ContinuationToken;

                    foreach (IListBlobItem blob in resultSegment.Results)
                    {
                        if (blob.GetType() == typeof(CloudBlockBlob))
                            allBlobs.Add(blob.Uri);
                    }
                } while (token != null);

                return View(allBlobs);
            }
            catch (Exception ex)
            {
                ViewData["message"] = ex.Message;
                ViewData["trace"] = ex.StackTrace;
                return View("Error");
            }
        }

        /// <summary> 
        /// Task<ActionResult> UploadAsync() 
        /// Documentation References:  
        /// - UploadFromFileAsync Method: https://msdn.microsoft.com/en-us/library/azure/microsoft.windowsazure.storage.blob.cloudpageblob.uploadfromfileasync.aspx
        /// </summary> 
        [HttpPost]
        public async Task<IActionResult> UploadAsync()
        {
            try
            {
                IFormFileCollection files = Request.Form.Files;
                int fileCount = files.Count;

                foreach (IFormFile formFile in files)
                {
                    CloudBlockBlob blob = blobContainer.GetBlockBlobReference(GetRandomBlobName(formFile.FileName));
                    using (var stream = formFile.OpenReadStream())
                    {
                        await blob.UploadFromStreamAsync(stream);
                    }
           
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewData["message"] = ex.Message;
                ViewData["trace"] = ex.StackTrace;
                return View("Error");
            }

            /*
            try
            {

                int fileCount = files.Count;

                foreach (IFormFile item in files)
                {
                    string fileName = ContentDispositionHeaderValue.Parse(item.ContentDisposition).FileName.Trim('"');
                    CloudBlockBlob blob = blobContainer.GetBlockBlobReference(GetRandomBlobName(fileName));
                    await blob.UploadFromFileAsync(fileName);
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewData["message"] = ex.Message;
                ViewData["trace"] = ex.StackTrace;
                return View("Error");
            }
            */
        }

        /// <summary> 
        /// Task<ActionResult> DeleteImage(string name) 
        /// Documentation References:  
        /// - Delete Blobs: https://azure.microsoft.com/en-us/documentation/articles/storage-dotnet-how-to-use-blobs/#delete-blobs
        /// </summary> 
        /// 
        [HttpPost]
        public async Task<ActionResult> DeleteImage(string name)
        {
            try
            {
                Uri uri = new Uri(name);
                string filename = Path.GetFileName(uri.LocalPath);

                var blob = blobContainer.GetBlockBlobReference(filename);
                await blob.DeleteIfExistsAsync();

                return RedirectToAction("SmartAlbumHome");
            }
            catch (Exception ex)
            {
                ViewData["message"] = ex.Message;
                ViewData["trace"] = ex.StackTrace;
                return View("Error");
            }
        }

        /// <summary> 
        /// Task<ActionResult> DeleteAll(string name) 
        /// Documentation References:  
        /// - Delete Blobs: https://azure.microsoft.com/en-us/documentation/articles/storage-dotnet-how-to-use-blobs/#delete-blobs
        /// </summary> 
        [HttpPost]
        public async Task<ActionResult> DeleteAll()
        {
            try
            {
                List<Uri> allBlobs = new List<Uri>();

                BlobContinuationToken token = null;
                do
                {
                    BlobResultSegment resultSegment =
                        await blobContainer.ListBlobsSegmentedAsync("", true,
                            new BlobListingDetails(), null, token, null, null);
                    token = resultSegment.ContinuationToken;

                    foreach (IListBlobItem blob in resultSegment.Results)
                    {
                        if (blob.GetType() == typeof(CloudBlockBlob))
                        {
                            await ((CloudBlockBlob)blob).DeleteIfExistsAsync();
                        }
                    }
                } while (token != null);


                return RedirectToAction("SmartAlbumHome");
            }
            catch (Exception ex)
            {
                ViewData["message"] = ex.Message;
                ViewData["trace"] = ex.StackTrace;
                return View("Error");
            }
        }

        /// <summary> 
        /// string GetRandomBlobName(string filename): Generates a unique random file name to be uploaded  
        /// </summary> 
        private string GetRandomBlobName(string filename)
        {
            string ext = Path.GetExtension(filename);
            return string.Format("{0:10}_{1}{2}", DateTime.Now.Ticks, Guid.NewGuid(), ext);
        }

        public ActionResult UploadPhotos()
        {
            return View();
        }
    }
}
