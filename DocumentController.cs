using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Web.Cross.Entities;
using Web.Domain.Entities;
using Web.Domain.Interfaces.InterfacesServices;
using Web.Domain.Pagination;
using Web.Domain.Views.Document;
using Web.Domain.Views.SupportingDocument;
using Web.Service.Helpers;

namespace Web.Api.Controllers
{
    [Route("api/[controller]")]
    public class DocumentController : BaseController
    {

        private readonly Session session;

        private readonly IConfiguration config;
        private readonly IDocumentService documentService;
        

        private enum TypeFrom
        {
           None,
           Indident,
           Kul
        }


        public DocumentController(IConfiguration config, IDocumentService documentService, Session session)
        {
            this.documentService= documentService;
            this.config = config;
            this.session = session;
            
        }


        // GET: IncidentController/Details/5
        [HttpGet("List/{incidentId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult GetIncidentModelAsync(long incidentId)
        {

            List<DocumentListModel> obj =  documentService.GetDocumentListModel(incidentId, Convert.ToInt32(TypeFrom.Indident));
            return Ok(obj);
        }


        // GET: IncidentController/Details/5
        [HttpGet("ListKul/{kulId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult GetKulModelAsync(long kulId)
        {

            List<DocumentListModel> obj = documentService.GetDocumentListModel(kulId, Convert.ToInt32(TypeFrom.Kul));
            return Ok(obj);
        }



        /// <summary>
        /// Eliminar cliente por ID Externo
        /// </summary>
        /// <param name="documentId"></param>
        [HttpDelete("{documentId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeleteAsync(long documentId)
        {
            Document document = new();
            document = await documentService.GetDocumentById(documentId);
            string fullPathFileName = string.Format("{0}{1}", document.Directory, document.DocumentId);
           // int deleteResult;
            try
            {
                string bucketName = config.GetValue<string>("Amazon:bucketName");
                string accessKeyID = Environment.GetEnvironmentVariable("AMZ_AK");
                string secretKey = Environment.GetEnvironmentVariable("AMZ_SK");
                string supportingDocumentFolder = config.GetValue<string>("Amazon:supportingDocumentFolder");
                var credentials = new Amazon.Runtime.BasicAWSCredentials(accessKeyID, secretKey);
                using var client = new AmazonS3Client(credentials, RegionEndpoint.USEast1);
                var fileTransferUtility = new TransferUtility(client);
                var deleteObjectRequest = new DeleteObjectRequest
                {
                    BucketName = bucketName,
                    Key = fullPathFileName
                };

                await fileTransferUtility.S3Client.DeleteObjectAsync(deleteObjectRequest);
                //deleteResult = 0;
                var result = await documentService.DeleteDocumentAsync(documentId);
                return StatusCode((int)result.StatusCode, result.Current);
            }
            catch (AmazonS3Exception ex)
            {

                //deleteResult = 1;
                return StatusCode((int)HttpStatusCode.BadRequest, ex);
            }
           
          
        }

        [HttpGet("DeleteFile")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        private async Task<SupportingDocumentIOResult> DeleteFile(string fullPathFileName)
        {
            SupportingDocumentIOResult IoResult = new();
            if (fullPathFileName == null)
            {
                IoResult.Code = (int)HttpStatusCode.BadRequest;
                IoResult.Status = false;
                IoResult.Error = "fullPathFileName is required";
                return IoResult;

            }

            try
            {
                string bucketName = config.GetValue<string>("Amazon:bucketName");
                string accessKeyID = Environment.GetEnvironmentVariable("AMZ_AK");
                string secretKey = Environment.GetEnvironmentVariable("AMZ_SK");
                string supportingDocumentFolder = config.GetValue<string>("Amazon:supportingDocumentFolder");
                var credentials = new Amazon.Runtime.BasicAWSCredentials(accessKeyID, secretKey);
                using var client = new AmazonS3Client(credentials, RegionEndpoint.USEast1);
                var fileTransferUtility = new TransferUtility(client);
                var deleteObjectRequest = new DeleteObjectRequest
                {
                    BucketName = bucketName,
                    Key = fullPathFileName
                };

                await fileTransferUtility.S3Client.DeleteObjectAsync(deleteObjectRequest);

                IoResult.Code = (int)HttpStatusCode.OK;
                IoResult.Status = true;
                IoResult.Error = null;
                return IoResult;

            }
            catch (AmazonS3Exception ex)
            {

                IoResult.Code = (int)HttpStatusCode.BadRequest;
                IoResult.Status = false;
                IoResult.Error = ex.Message;
                return IoResult;
            }



        }



        /// <summary>
        /// Upload file to S3
        /// </summary>
        /// <param name="myfile"></param>
        /// <param name="crid"></param>
        /// <param name="indidentDetailId"></param>
        /// <returns></returns>
        [HttpPost()]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [DisableRequestSizeLimit]
        public async Task<SupportingDocumentIOResult> PostAsync(IFormFile myfile,  long incidentId)
        {
            SupportingDocumentIOResult IoResult = new();
            string keyname;
            try
            {
                string extension = myfile.FileName[(myfile.FileName.LastIndexOf('.') + 1)..];
                List<DocumentListModel> fileStoredOriginal = documentService.GetDocumentListModel(incidentId, Convert.ToInt32(TypeFrom.Indident));
                int i = 0;
                string fileName = myfile.FileName;
                while (fileStoredOriginal.Exists(x => x.FileName == fileName))
                {
                    if (i == 0)
                        fileName = fileName.Replace("." + extension, "(" + ++i + ")." + extension);
                    else
                        fileName = fileName.Replace("(" + i + ")." + extension, "(" + ++i + ")." + extension);
                }
                string uploadFileName = fileName;

                string bucketName = config.GetValue<string>("Amazon:bucketName");
                string accessKeyID = Environment.GetEnvironmentVariable("AMZ_AK");
                string secretKey = Environment.GetEnvironmentVariable("AMZ_SK");
                int maxKeys = Convert.ToInt32(config.GetValue<string>("Amazon:maxKeys"));
                string supportingDocumentFolder = config.GetValue<string>("Amazon:supportingDocumentFolder");

                var credentials = new Amazon.Runtime.BasicAWSCredentials(accessKeyID, secretKey);
                string path = string.Format(@"{0}/{1}/", supportingDocumentFolder, incidentId);
                string task = await CreateFoldersAsync(path, TypeFrom.Indident);

                if (task != "Ok")
                {
                    IoResult.Code = (int)HttpStatusCode.BadRequest;
                    IoResult.Status = false;
                    IoResult.Error = task;
                    return IoResult;
                }

                //Create document in table
                Document document = new Document
                {
                    BucketName = bucketName,
                    Directory = path,
                    DocumentOwnerId = 1,
                    CreatedAt = DateTime.Now,
                    CreatedBy = session.UserId.ToString(),
                    FileName = uploadFileName,
                    IncidentId = incidentId,
                    FromType = Convert.ToInt32(TypeFrom.Indident),
                    Size = System.Math.Round(System.Convert.ToDecimal(myfile.Length / 1024), 2)
                };
                long documentIdResult = await documentService.GetInsertedDocumentId(document);

                using var client = new AmazonS3Client(credentials, RegionEndpoint.USEast1);
                keyname = path + documentIdResult.ToString();

                var fs = myfile.OpenReadStream();
                var request = new Amazon.S3.Model.PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = keyname,
                    InputStream = fs,
                    ContentType = myfile.ContentType,
                    CannedACL = S3CannedACL.PublicRead
                };
                await client.PutObjectAsync(request);

                IoResult.Code = (int)HttpStatusCode.OK;
                IoResult.Status = true;
                IoResult.Error = null;






                return IoResult;



            }
            catch (Exception ex)
            {
                IoResult.Code = (int)HttpStatusCode.BadRequest;
                IoResult.Status = false;
                IoResult.Error = ex.Message;
                return IoResult;
            }

        }



        /// <summary>
        /// Upload file to S3
        /// </summary>
        /// <param name="myfile"></param>
        /// <param name="kulId"></param>
        /// <returns></returns>
        [HttpPost("UploadDocumentKUL")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [DisableRequestSizeLimit]
        public async Task<SupportingDocumentIOResult> UploadDocumentKUL(IFormFile myfile, long kulId)
        {
            SupportingDocumentIOResult IoResult = new();
            string keyname;
            try
            {
                string extension = myfile.FileName[(myfile.FileName.LastIndexOf('.') + 1)..];
                List<DocumentListModel> fileStoredOriginal = documentService.GetDocumentListModel(kulId, Convert.ToInt32(TypeFrom.Kul));
                int i = 0;
                string fileName = myfile.FileName;
                while (fileStoredOriginal.Exists(x => x.FileName == fileName))
                {
                    if (i == 0)
                        fileName = fileName.Replace("." + extension, "(" + ++i + ")." + extension);
                    else
                        fileName = fileName.Replace("(" + i + ")." + extension, "(" + ++i + ")." + extension);
                }
                string uploadFileName = fileName;

                string bucketName = config.GetValue<string>("Amazon:bucketName");
                string accessKeyID = config.GetValue<string>("Amazon:accessKeyID");
                string secretKey = config.GetValue<string>("Amazon:secretKey");
                int maxKeys = Convert.ToInt32(config.GetValue<string>("Amazon:maxKeys"));
                string supportingDocumentFolder = config.GetValue<string>("Amazon:supportingDocumentFolderKul");

                var credentials = new Amazon.Runtime.BasicAWSCredentials(accessKeyID, secretKey);
                string path = string.Format(@"{0}/{1}/", supportingDocumentFolder, kulId);
                string task = await CreateFoldersAsync(path, TypeFrom.Kul);

                if (task != "Ok")
                {
                    IoResult.Code = (int)HttpStatusCode.BadRequest;
                    IoResult.Status = false;
                    IoResult.Error = task;
                    return IoResult;
                }

                //Create document in table
                Document document = new Document
                {
                    BucketName = bucketName,
                    Directory = path,
                    DocumentOwnerId = 2,
                    CreatedAt = DateTime.Now,
                    CreatedBy = session.UserId.ToString(),
                    FileName = uploadFileName,
                    IncidentId = kulId,
                    FromType = Convert.ToInt32(TypeFrom.Kul),
                    Size = System.Math.Round(System.Convert.ToDecimal(myfile.Length / 1024), 2)
                };
                long documentIdResult = await documentService.GetInsertedDocumentId(document);

                using var client = new AmazonS3Client(credentials, RegionEndpoint.USEast1);
                keyname = path + documentIdResult.ToString();

                var fs = myfile.OpenReadStream();
                var request = new Amazon.S3.Model.PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = keyname,
                    InputStream = fs,
                    ContentType = myfile.ContentType,
                    CannedACL = S3CannedACL.PublicRead
                };
                await client.PutObjectAsync(request);

                IoResult.Code = (int)HttpStatusCode.OK;
                IoResult.Status = true;
                IoResult.Error = null;

                return IoResult;


            }
            catch (Exception ex)
            {
                IoResult.Code = (int)HttpStatusCode.BadRequest;
                IoResult.Status = false;
                IoResult.Error = ex.Message;
                return IoResult;
            }

        }





        [HttpGet("DownloadFile")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DownloadFile(long crid, long incidentId, long documentId)
        {
            Document doc = await documentService.GetDocumentById(documentId);
            string originalFileName = doc.FileName;
            SupportingDocumentIOResult IoResult = new();
            if (originalFileName == null)
            {
                IoResult.Code = (int)HttpStatusCode.BadRequest;
                IoResult.Status = false;
                IoResult.Error = "originalFileName is required";
                return BadRequest(IoResult);
            }

            try
            {
                string bucketName = config.GetValue<string>("Amazon:bucketName");
                string accessKeyID = Environment.GetEnvironmentVariable("AMZ_AK");
                string secretKey = Environment.GetEnvironmentVariable("AMZ_SK");
                string supportingDocumentFolder = config.GetValue<string>("Amazon:supportingDocumentFolder");
                var credentials = new Amazon.Runtime.BasicAWSCredentials(accessKeyID, secretKey);
                using var client = new AmazonS3Client(credentials, RegionEndpoint.USEast1);
                var fileTransferUtility = new TransferUtility(client);
                var objectResponse = await fileTransferUtility.S3Client.GetObjectAsync(new GetObjectRequest()
                {
                    BucketName = bucketName,
                    Key = string.Format("{0}/{1}/{2}", supportingDocumentFolder, incidentId, documentId)
                });
                if (objectResponse.ResponseStream == null)
                {
                    client.Dispose();
                    return NotFound();
                }
                return File(objectResponse.ResponseStream, objectResponse.Headers.ContentType, originalFileName);
            }
            catch (AmazonS3Exception ex)
            {

                IoResult.Code = (int)ex.StatusCode;
                IoResult.Status = false;
                IoResult.Error = ex.Message;

                return BadRequest(IoResult);
            }



        }



        [HttpGet("DownloadFileKul")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DownloadFileKul(long kulId, long documentId)
        {
            Document doc = await documentService.GetDocumentById(documentId);
            string originalFileName = doc.FileName;
            SupportingDocumentIOResult IoResult = new();
            if (originalFileName == null)
            {
                IoResult.Code = (int)HttpStatusCode.BadRequest;
                IoResult.Status = false;
                IoResult.Error = "originalFileName is required";
                return BadRequest(IoResult);
            }

            try
            {
                string bucketName = config.GetValue<string>("Amazon:bucketName");
                string accessKeyID = config.GetValue<string>("Amazon:accessKeyID");
                string secretKey = config.GetValue<string>("Amazon:secretKey");
                string supportingDocumentFolder = config.GetValue<string>("Amazon:supportingDocumentFolderKul");
                var credentials = new Amazon.Runtime.BasicAWSCredentials(accessKeyID, secretKey);
                using var client = new AmazonS3Client(credentials, RegionEndpoint.USEast1);
                var fileTransferUtility = new TransferUtility(client);
                var objectResponse = await fileTransferUtility.S3Client.GetObjectAsync(new GetObjectRequest()
                {
                    BucketName = bucketName,
                    Key = string.Format("{0}/{1}/{2}", supportingDocumentFolder, kulId, documentId)
                });
                if (objectResponse.ResponseStream == null)
                {
                    client.Dispose();
                    return NotFound();
                }
                return File(objectResponse.ResponseStream, objectResponse.Headers.ContentType, originalFileName);
            }
            catch (AmazonS3Exception ex)
            {

                IoResult.Code = (int)ex.StatusCode;
                IoResult.Status = false;
                IoResult.Error = ex.Message;

                return BadRequest(IoResult);
            }



        }

        [HttpPost("[action]")]
        public ActionResult PaginatedList([FromQuery] PageParams pageParam, [FromBody] long incidentId)
        {
            var response =  documentService.GetPaginateDocumentListModel(pageParam,incidentId, Convert.ToInt32(TypeFrom.Indident));
            return Ok(response);
        }


        [HttpPost("[action]")]
        public ActionResult PaginatedListKul([FromQuery] PageParams pageParam, [FromBody] long kulId)
        {
            var response = documentService.GetPaginateDocumentListModel(pageParam, kulId, Convert.ToInt32(TypeFrom.Kul));
            return Ok(response);
        }



        /// <summary>
        /// To create folder on S3 if no exists
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private async Task<string> CreateFoldersAsync(string path, TypeFrom fromType )
        {


            if (!path.EndsWith('/'))
            {
                return @"Error, path must end with '/'";

            }
            string bucketName = config.GetValue<string>("Amazon:bucketName");
            string accessKeyID = Environment.GetEnvironmentVariable("AMZ_AK");
            string secretKey = Environment.GetEnvironmentVariable("AMZ_SK");
            string supportingDocumentFolder = config.GetValue<string>("Amazon:supportingDocumentFolder");
            

            int maxKeys = Convert.ToInt32(config.GetValue<string>("Amazon:maxKeys"));
            var credentials = new Amazon.Runtime.BasicAWSCredentials(accessKeyID, secretKey);

            using var client = new AmazonS3Client(credentials, RegionEndpoint.USEast1);

            var findFolderRequest = new ListObjectsV2Request();
            findFolderRequest.BucketName = bucketName;
            findFolderRequest.Prefix = path;

            ListObjectsV2Response findFolderResponse =
               await client.ListObjectsV2Async(findFolderRequest);


            if (findFolderResponse.S3Objects.Any())
            {
                client.Dispose();
                return "Ok";
            }

            PutObjectRequest request = new()
            {
                BucketName = bucketName,
                StorageClass = S3StorageClass.Standard,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.None,
                Key = path,
                ContentBody = string.Empty
            };

            // add try catch in case you have exceptions shield/handling here 
            try
            {
                PutObjectResponse response = await client.PutObjectAsync(request);
                client.Dispose();
                return "Ok";
            }
            catch (Exception ex)
            {
                client.Dispose();
                return ex.Message;
            }

        }


    



        //[HttpGet("xSelectDocumentListModelAllToDeleteASync")]
        //public async Task<ActionResult> xSelectDocumentListModelAllToDeleteASyncAsync()
        //{
        //    List<String> result = new();
        //    List<DocumentListModel> documentListModels = new List<DocumentListModel>();
        //   documentListModels = documentService.SelectDocumentListModelAllToDeleteASync();
        //    foreach (DocumentListModel item in documentListModels)
        //    {
        //        Document document = new();
        //        document = await documentService.GetDocumentById(item.DocumentId);
        //        string fullPathFileName = string.Format("{0}{1}", document.Directory, document.DocumentId);

        //        // int deleteResult;
        //        try
        //        {
        //            string bucketName = config.GetValue<string>("Amazon:bucketName");
        //            string accessKeyID = config.GetValue<string>("Amazon:accessKeyID");
        //            string secretKey = config.GetValue<string>("Amazon:secretKey");
        //            string supportingDocumentFolder = config.GetValue<string>("Amazon:supportingDocumentFolder");
        //            var credentials = new Amazon.Runtime.BasicAWSCredentials(accessKeyID, secretKey);
        //            using var client = new AmazonS3Client(credentials, RegionEndpoint.USEast1);
        //            var fileTransferUtility = new TransferUtility(client);
        //            var deleteObjectRequest = new DeleteObjectRequest
        //            {
        //                BucketName = bucketName,
        //                Key = fullPathFileName
        //            };

        //          //  await fileTransferUtility.S3Client.DeleteObjectAsync(deleteObjectRequest);
        //            //deleteResult = 0;
        //        //    var resultado = await documentService.DeleteDocumentAsync(item.DocumentId);
        //            result.Add($"Eliminado. IdDocument:{item.DocumentId} - fileName:{item.FileName} ");
        //        }
        //        catch (AmazonS3Exception ex)
        //        {

        //            result.Add($"Fallo. IdDocument:{item.DocumentId} - fileName:{item.FileName}  error:{ex.Message} ");
        //        }
        //    }

           


        //    return Ok(result);
        //}


        

    }
}
