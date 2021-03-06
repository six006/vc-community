﻿using System.Linq;
using System.Net;
using System.Web.Http;
using System.Web.Http.Description;
using VirtoCommerce.Domain.Payment.Services;
using VirtoCommerce.Domain.Shipping.Services;
using VirtoCommerce.Domain.Store.Services;
using VirtoCommerce.Domain.Tax.Services;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.StoreModule.Web.Converters;
using VirtoCommerce.StoreModule.Web.Security;
using coreModel = VirtoCommerce.Domain.Store.Model;
using webModel = VirtoCommerce.StoreModule.Web.Model;

namespace VirtoCommerce.StoreModule.Web.Controllers.Api
{
	[RoutePrefix("api/stores")]
	public class StoreModuleController : ApiController
	{
		private readonly IStoreService _storeService;
		private readonly IShippingService _shippingService;
		private readonly IPaymentMethodsService _paymentService;
        private readonly ITaxService _taxService;
        private readonly ISecurityService _securityService;
        private readonly IPermissionScopeService _permissionScopeService;
		public StoreModuleController(IStoreService storeService, IShippingService shippingService, IPaymentMethodsService paymentService, ITaxService taxService, 
                                     ISecurityService securityService, IPermissionScopeService permissionScopeService)
		{
			_storeService = storeService;
			_shippingService = shippingService;
			_paymentService = paymentService;
            _taxService = taxService;
            _securityService = securityService;
            _permissionScopeService = permissionScopeService;
        }

		/// <summary>
		/// Get all stores
		/// </summary>
		[HttpGet]
		[ResponseType(typeof(webModel.Store[]))]
		[Route("")]
        [OverrideAuthorization]
		public IHttpActionResult GetStores()
		{
            var retVal = _storeService.GetStoreList().Select(x => x.ToWebModel()).ToArray();
            //Filter resulting stores correspond to current user permissions
            //first check global permission
            if (!_securityService.UserHasAnyPermission(User.Identity.Name, null, StorePredefinedPermissions.Read))
            {
                //Get user 'read' permission scopes
                var selectedStoreScopes = _securityService.GetUserPermissions(User.Identity.Name)
                                                      .Where(x => x.Id.StartsWith(StorePredefinedPermissions.Read))
                                                      .SelectMany(x => x.AssignedScopes)
                                                      .OfType<StoreSelectedScope>()
                                                      .Select(x => x.Scope)
                                                      .ToArray();
                retVal = retVal.Where(x => selectedStoreScopes.Contains(x.Id)).ToArray();
            }
            return Ok(retVal);
		}

		/// <summary>
		/// Get store by id
		/// </summary>
		/// <param name="id">Store id</param>
		/// <responce code="404">Store not found</responce>
		/// <responce code="200">Store returned successfully OK</responce>
		[HttpGet]
		[ResponseType(typeof(webModel.Store))]
		[Route("{id}")]
		public IHttpActionResult GetStoreById(string id)
		{
			var store = _storeService.GetById(id);
			if(store == null)
			{
				return NotFound();
			}
            CheckCurrentUserHasPermissionForObjects(StorePredefinedPermissions.Read, store);
            var retVal = store.ToWebModel();
            retVal.SecurityScopes = _permissionScopeService.GetObjectPermissionScopeStrings(store).ToArray();
			return Ok(retVal);
		}

		
		/// <summary>
		/// Create store
		/// </summary>
		/// <param name="store">Store</param>
		[HttpPost]
		[ResponseType(typeof(webModel.Store))]
		[Route("")]
        [CheckPermission(Permission = StorePredefinedPermissions.Create)]
		public IHttpActionResult Create(webModel.Store store)
		{
			var coreStore = store.ToCoreModel(_shippingService.GetAllShippingMethods(), _paymentService.GetAllPaymentMethods(), _taxService.GetAllTaxProviders());
			var retVal = _storeService.Create(coreStore);
			return Ok(retVal.ToWebModel());
		}

		/// <summary>
		/// Update store
		/// </summary>
		/// <param name="store">Store</param>
		[HttpPut]
		[ResponseType(typeof(void))]
		[Route("")]
		public IHttpActionResult Update(webModel.Store store)
		{
			var coreStore = store.ToCoreModel(_shippingService.GetAllShippingMethods(), _paymentService.GetAllPaymentMethods(), _taxService.GetAllTaxProviders());
            CheckCurrentUserHasPermissionForObjects(StorePredefinedPermissions.Update, coreStore);
            _storeService.Update(new coreModel.Store[] { coreStore });
			return StatusCode(HttpStatusCode.NoContent);
		}

		/// <summary>
		/// Delete stores
		/// </summary>
		/// <param name="ids">Ids of store that needed to delete</param>
		[HttpDelete]
		[ResponseType(typeof(void))]
		[Route("")]
		public IHttpActionResult Delete([FromUri] string[] ids)
		{
            var stores = ids.Select(x => _storeService.GetById(x)).ToArray();
            CheckCurrentUserHasPermissionForObjects(StorePredefinedPermissions.Delete, stores);
            _storeService.Delete(ids);
			return StatusCode(HttpStatusCode.NoContent);
		}

        protected void CheckCurrentUserHasPermissionForObjects(string permission, params object[] objects)
        {
            //Scope bound security check
            var scopes = objects.SelectMany(x => _permissionScopeService.GetObjectPermissionScopeStrings(x)).Distinct().ToArray();
            if (!_securityService.UserHasAnyPermission(User.Identity.Name, scopes, permission))
            {
                throw new HttpResponseException(HttpStatusCode.Unauthorized);
            }
        }
    }
}
