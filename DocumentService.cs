using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Web.Domain.Entities;
using Web.Domain.Helpers;
using Web.Domain.Interfaces.InterfacesRepository;
using Web.Domain.Interfaces.InterfacesServices;
using Web.Domain.Pagination;
using Web.Domain.Views.Document;

namespace Web.Service.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository documentRepository;
        private readonly ILogger<IncidentService> logger;

        public DocumentService(IDocumentRepository documentRepository, ILogger<IncidentService> logger)
        {
            this.documentRepository = documentRepository;
            this.logger = logger;
        }



        public async Task<Result<long, List<Tuple<string, string>>>> AddDocumentAsync(Document objEntityModel)
        {


            try
            {

                var objId = await documentRepository.InsertASync(objEntityModel);
                return Result<long, List<Tuple<string, string>>>.NewSuccess(objId);
            }
            catch (Exception ex)
            {
                List<Tuple<string, string>> exError = new();
                exError.Add(new Tuple<string, string>("DocumentId", ex.ToString()));
                logger.LogError(ex.ToString());
                return Result<long, List<Tuple<string, string>>>.NewError(exError, HttpStatusCode.BadRequest);
            }

        }

        public async Task<long> GetInsertedDocumentId(Document objEntityModel)
        {

                var objId = await documentRepository.InsertASync(objEntityModel);
                return objId;
        }



        public async Task<Result<long, List<Tuple<string, string>>>> DeleteDocumentAsync(long documentId)
        {


            try
            {
                await documentRepository.RemoveASync(documentId);
                return Result<long, List<Tuple<string, string>>>.NewSuccess(documentId);
            }
            catch (Exception ex)
            {
                List<Tuple<string, string>> exError = new();
                exError.Add(new Tuple<string, string>("DocumentId", ex.ToString()));
                logger.LogError(ex.ToString());
                return Result<long, List<Tuple<string, string>>>.NewError(exError, HttpStatusCode.BadRequest);
            }
        }


        public List<DocumentListModel> GetDocumentListModel(long incidentId, int fromType)
        {
            //documentOwner 1=Incident
            List<DocumentListModel> list = documentRepository.SelectDocumentListModelAllASync(incidentId, fromType);
            return list;
        }



        public PageResult GetPaginateDocumentListModel(PageParams pageParams, long incidentId, int fromType)
        {
            //documentOwner 1=Incident
            PagingList<DocumentListModel> documentListModels = documentRepository.SelectIncidentListModelPagination(pageParams, incidentId, fromType);

            PageResult listPaginateResult = new()
            {
                ResultList = documentListModels,
                Capacity = documentListModels.Capacity,
                HasNextPage = documentListModels.HasNextPage,
                HasPreviousPage = documentListModels.HasPreviousPage,
                TotalPageNo = documentListModels.TotalPageNo,
                PageIndex = documentListModels.PageIndex,
                TotalRecords = documentListModels.Count
            };
            return listPaginateResult;
        }


        public async Task<Document> GetDocumentById(long documentId)
        {
            return await documentRepository.SelectByIdASync(documentId);
        }


        public async Task<List<Document>> SelectDocumentListModelAllToDeleteASync()
        {
            //documentOwner 1=Incident
            List<Document> list = await documentRepository.SelectDocumentListModelAllToDeleteASync();
            return list;
        }
    }
}
