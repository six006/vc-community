﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.MarketingModule.Data.Model
{
	public class PublishingGroupContentPlace : AuditableEntity
	{
		#region Navigation Properties
		[StringLength(128)]
		public string DynamicContentPublishingGroupId { get; set; }

		public virtual DynamicContentPublishingGroup PublishingGroup { get; set; }

		[StringLength(128)]
		public string DynamicContentPlaceId { get; set; }
		public virtual DynamicContentPlace ContentPlace { get; set; }
				
		#endregion
	}
}
