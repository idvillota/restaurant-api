using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Models;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;

namespace Restaurant.Infrastructure.Common;

internal static class PagedEntityQueries
{
    public static IQueryable<Ingredient> ShapeIngredients(IQueryable<Ingredient> query, ListQuery q)
    {
        query = query.Include(i => i.IngredientCategory);

        if (!q.IncludeInactive)
            query = query.Where(i => i.IsActive);

        var search = ListQueryHelpers.SearchTerm(q);
        if (search.Length > 0)
        {
            query = query.Where(i =>
                i.Name.ToLower().Contains(search) ||
                i.IngredientCategory.Name.ToLower().Contains(search));
        }

        if (q.FilterValue("name") is { } nameFilter)
            query = query.Where(i => i.Name.ToLower().Contains(nameFilter.ToLowerInvariant()));

        if (q.FilterValue("ingredientCategoryName") is { } catName)
            query = query.Where(i => i.IngredientCategory.Name.ToLower().Contains(catName.ToLowerInvariant()));

        if (q.FilterValue("ingredientCategoryId") is { } catId &&
            ListQueryHelpers.TryParseGuid(catId, out var categoryId))
            query = query.Where(i => i.IngredientCategoryId == categoryId);

        if (q.FilterValue("unit") is { } unitRaw &&
            Enum.TryParse<IngredientUnit>(unitRaw, true, out var unit))
            query = query.Where(i => i.Unit == unit);

        if (q.FilterValue("unitCost") is { } unitCostRaw &&
            ListQueryHelpers.TryParseDecimal(unitCostRaw, out var unitCost))
            query = query.Where(i => i.UnitCost == unitCost);

        if (q.FilterValue("stockQuantity") is { } stockRaw &&
            ListQueryHelpers.TryParseDecimal(stockRaw, out var stock))
            query = query.Where(i => i.StockQuantity == stock);

        if (q.FilterValue("reorderLevel") is { } reorderRaw &&
            ListQueryHelpers.TryParseDecimal(reorderRaw, out var reorder))
            query = query.Where(i => i.ReorderLevel == reorder);

        if (q.FilterValue("isActive") is { } activeRaw &&
            ListQueryHelpers.TryParseBool(activeRaw, out var isActive))
            query = query.Where(i => i.IsActive == isActive);

        return ApplyIngredientSort(query, q);
    }

    public static IQueryable<IngredientCategory> ShapeIngredientCategories(IQueryable<IngredientCategory> query, ListQuery q)
    {
        if (!q.IncludeInactive)
            query = query.Where(c => c.IsActive);

        var search = ListQueryHelpers.SearchTerm(q);
        if (search.Length > 0)
        {
            query = query.Where(c =>
                c.Name.ToLower().Contains(search) ||
                (c.Description != null && c.Description.ToLower().Contains(search)));
        }

        if (q.FilterValue("name") is { } nameFilter)
            query = query.Where(c => c.Name.ToLower().Contains(nameFilter.ToLowerInvariant()));

        if (q.FilterValue("description") is { } descFilter)
            query = query.Where(c => c.Description != null && c.Description.ToLower().Contains(descFilter.ToLowerInvariant()));

        if (q.FilterValue("sortOrder") is { } sortRaw &&
            ListQueryHelpers.TryParseInt(sortRaw, out var sortOrder))
            query = query.Where(c => c.SortOrder == sortOrder);

        if (q.FilterValue("isActive") is { } activeRaw &&
            ListQueryHelpers.TryParseBool(activeRaw, out var isActive))
            query = query.Where(c => c.IsActive == isActive);

        return ApplyIngredientCategorySort(query, q);
    }

    public static IQueryable<Product> ShapeProducts(IQueryable<Product> query, ListQuery q)
    {
        query = query.Include(p => p.ProductType);

        if (!q.IncludeInactive)
            query = query.Where(p => p.IsActive);

        var search = ListQueryHelpers.SearchTerm(q);
        if (search.Length > 0)
        {
            query = query.Where(p =>
                p.Name.ToLower().Contains(search) ||
                (p.Description != null && p.Description.ToLower().Contains(search)) ||
                (p.Sku != null && p.Sku.ToLower().Contains(search)) ||
                p.ProductType.Name.ToLower().Contains(search));
        }

        if (q.FilterValue("name") is { } nameFilter)
            query = query.Where(p => p.Name.ToLower().Contains(nameFilter.ToLowerInvariant()));

        if (q.FilterValue("description") is { } descFilter)
            query = query.Where(p => p.Description != null && p.Description.ToLower().Contains(descFilter.ToLowerInvariant()));

        if (q.FilterValue("sku") is { } skuFilter)
            query = query.Where(p => p.Sku != null && p.Sku.ToLower().Contains(skuFilter.ToLowerInvariant()));

        if (q.FilterValue("productTypeName") is { } typeName)
            query = query.Where(p => p.ProductType.Name.ToLower().Contains(typeName.ToLowerInvariant()));

        if (q.FilterValue("productTypeId") is { } typeIdRaw &&
            ListQueryHelpers.TryParseGuid(typeIdRaw, out var typeId))
            query = query.Where(p => p.ProductTypeId == typeId);

        if (q.FilterValue("unitPrice") is { } priceRaw &&
            ListQueryHelpers.TryParseDecimal(priceRaw, out var unitPrice))
            query = query.Where(p => p.UnitPrice == unitPrice);

        if (q.FilterValue("isActive") is { } activeRaw &&
            ListQueryHelpers.TryParseBool(activeRaw, out var isActive))
            query = query.Where(p => p.IsActive == isActive);

        if (q.FilterValue("compositionType") is { } kindRaw &&
            Enum.TryParse<EProductType>(kindRaw, ignoreCase: true, out var compositionType))
            query = query.Where(p => p.CompositionType == compositionType);

        return ApplyProductSort(query, q);
    }

    public static IQueryable<ProductType> ShapeProductTypes(IQueryable<ProductType> query, ListQuery q)
    {
        if (!q.IncludeInactive)
            query = query.Where(t => t.IsActive);

        var search = ListQueryHelpers.SearchTerm(q);
        if (search.Length > 0)
        {
            query = query.Where(t =>
                t.Name.ToLower().Contains(search) ||
                (t.Description != null && t.Description.ToLower().Contains(search)));
        }

        if (q.FilterValue("name") is { } nameFilter)
            query = query.Where(t => t.Name.ToLower().Contains(nameFilter.ToLowerInvariant()));

        if (q.FilterValue("description") is { } descFilter)
            query = query.Where(t => t.Description != null && t.Description.ToLower().Contains(descFilter.ToLowerInvariant()));

        if (q.FilterValue("sortOrder") is { } sortRaw &&
            ListQueryHelpers.TryParseInt(sortRaw, out var sortOrder))
            query = query.Where(t => t.SortOrder == sortOrder);

        if (q.FilterValue("isActive") is { } activeRaw &&
            ListQueryHelpers.TryParseBool(activeRaw, out var isActive))
            query = query.Where(t => t.IsActive == isActive);

        return ApplyProductTypeSort(query, q);
    }

    public static IQueryable<Provider> ShapeProviders(IQueryable<Provider> query, ListQuery q)
    {
        if (!q.IncludeInactive)
            query = query.Where(p => p.IsActive);

        var search = ListQueryHelpers.SearchTerm(q);
        if (search.Length > 0)
        {
            query = query.Where(p =>
                p.Name.ToLower().Contains(search) ||
                (p.Email != null && p.Email.ToLower().Contains(search)) ||
                (p.Phone != null && p.Phone.ToLower().Contains(search)) ||
                (p.TaxId != null && p.TaxId.ToLower().Contains(search)) ||
                (p.Notes != null && p.Notes.ToLower().Contains(search)));
        }

        ApplyProviderFilters(ref query, q);
        return ApplyProviderSort(query, q);
    }

    public static IQueryable<Employee> ShapeEmployees(IQueryable<Employee> query, ListQuery q)
    {
        if (!q.IncludeInactive)
            query = query.Where(e => e.IsActive);

        var search = ListQueryHelpers.SearchTerm(q);
        if (search.Length > 0)
        {
            query = query.Where(e =>
                e.FullName.ToLower().Contains(search) ||
                (e.JobTitle != null && e.JobTitle.ToLower().Contains(search)));
        }

        if (q.FilterValue("fullName") is { } nameFilter)
            query = query.Where(e => e.FullName.ToLower().Contains(nameFilter.ToLowerInvariant()));

        if (q.FilterValue("jobTitle") is { } titleFilter)
            query = query.Where(e => e.JobTitle != null && e.JobTitle.ToLower().Contains(titleFilter.ToLowerInvariant()));

        if (q.FilterValue("isActive") is { } activeRaw &&
            ListQueryHelpers.TryParseBool(activeRaw, out var isActive))
            query = query.Where(e => e.IsActive == isActive);

        return ApplyEmployeeSort(query, q);
    }

    public static IQueryable<DiningTable> ShapeDiningTables(IQueryable<DiningTable> query, ListQuery q)
    {
        if (!q.IncludeInactive)
            query = query.Where(t => t.IsActive);

        var search = ListQueryHelpers.SearchTerm(q);
        if (search.Length > 0)
        {
            query = query.Where(t =>
                t.Code.ToLower().Contains(search) ||
                (t.Zone != null && t.Zone.ToLower().Contains(search)));
        }

        if (q.FilterValue("code") is { } codeFilter)
            query = query.Where(t => t.Code.ToLower().Contains(codeFilter.ToLowerInvariant()));

        if (q.FilterValue("zone") is { } zoneFilter)
            query = query.Where(t => t.Zone != null && t.Zone.ToLower().Contains(zoneFilter.ToLowerInvariant()));

        if (q.FilterValue("capacity") is { } capRaw &&
            ListQueryHelpers.TryParseInt(capRaw, out var capacity))
            query = query.Where(t => t.Capacity == capacity);

        if (q.FilterValue("isActive") is { } activeRaw &&
            ListQueryHelpers.TryParseBool(activeRaw, out var isActive))
            query = query.Where(t => t.IsActive == isActive);

        if (q.FilterValue("status") is { } statusRaw &&
            Enum.TryParse<ETableStatus>(statusRaw, ignoreCase: true, out var tableStatus))
            query = query.Where(t => t.Status == tableStatus);

        return ApplyDiningTableSort(query, q);
    }

    public static IQueryable<Customer> ShapeCustomers(IQueryable<Customer> query, ListQuery q)
    {
        if (!q.IncludeInactive)
            query = query.Where(c => c.IsActive);

        var search = ListQueryHelpers.SearchTerm(q);
        if (search.Length > 0)
        {
            query = query.Where(c =>
                c.Name.ToLower().Contains(search) ||
                (c.Email != null && c.Email.ToLower().Contains(search)) ||
                (c.Phone != null && c.Phone.ToLower().Contains(search)) ||
                (c.TaxId != null && c.TaxId.ToLower().Contains(search)));
        }

        if (q.FilterValue("name") is { } nameFilter)
            query = query.Where(c => c.Name.ToLower().Contains(nameFilter.ToLowerInvariant()));

        if (q.FilterValue("email") is { } emailFilter)
            query = query.Where(c => c.Email != null && c.Email.ToLower().Contains(emailFilter.ToLowerInvariant()));

        if (q.FilterValue("phone") is { } phoneFilter)
            query = query.Where(c => c.Phone != null && c.Phone.ToLower().Contains(phoneFilter.ToLowerInvariant()));

        if (q.FilterValue("taxId") is { } taxFilter)
            query = query.Where(c => c.TaxId != null && c.TaxId.ToLower().Contains(taxFilter.ToLowerInvariant()));

        if (q.FilterValue("isActive") is { } activeRaw &&
            ListQueryHelpers.TryParseBool(activeRaw, out var isActive))
            query = query.Where(c => c.IsActive == isActive);

        return ApplyCustomerSort(query, q);
    }

    public static IQueryable<Reservation> ShapeReservations(IQueryable<Reservation> query, ListQuery q)
    {
        query = query.Include(r => r.Customer);

        var search = ListQueryHelpers.SearchTerm(q);
        if (search.Length > 0)
        {
            query = query.Where(r =>
                r.ContactName.ToLower().Contains(search) ||
                (r.ContactEmail != null && r.ContactEmail.ToLower().Contains(search)) ||
                (r.ContactPhone != null && r.ContactPhone.ToLower().Contains(search)) ||
                (r.Notes != null && r.Notes.ToLower().Contains(search)) ||
                (r.Customer != null && r.Customer.Name.ToLower().Contains(search)));
        }

        if (q.FilterValue("contactName") is { } nameFilter)
            query = query.Where(r => r.ContactName.ToLower().Contains(nameFilter.ToLowerInvariant()));

        if (q.FilterValue("contactEmail") is { } emailFilter)
            query = query.Where(r => r.ContactEmail != null && r.ContactEmail.ToLower().Contains(emailFilter.ToLowerInvariant()));

        if (q.FilterValue("contactPhone") is { } phoneFilter)
            query = query.Where(r => r.ContactPhone != null && r.ContactPhone.ToLower().Contains(phoneFilter.ToLowerInvariant()));

        if (q.FilterValue("partySize") is { } partyRaw &&
            ListQueryHelpers.TryParseInt(partyRaw, out var partySize))
            query = query.Where(r => r.PartySize == partySize);

        if (q.FilterValue("status") is { } statusRaw &&
            Enum.TryParse<ReservationStatus>(statusRaw, true, out var status))
            query = query.Where(r => r.Status == status);

        return ApplyReservationSort(query, q);
    }

    private static void ApplyProviderFilters(ref IQueryable<Provider> query, ListQuery q)
    {
        if (q.FilterValue("name") is { } nameFilter)
            query = query.Where(p => p.Name.ToLower().Contains(nameFilter.ToLowerInvariant()));

        if (q.FilterValue("email") is { } emailFilter)
            query = query.Where(p => p.Email != null && p.Email.ToLower().Contains(emailFilter.ToLowerInvariant()));

        if (q.FilterValue("phone") is { } phoneFilter)
            query = query.Where(p => p.Phone != null && p.Phone.ToLower().Contains(phoneFilter.ToLowerInvariant()));

        if (q.FilterValue("taxId") is { } taxFilter)
            query = query.Where(p => p.TaxId != null && p.TaxId.ToLower().Contains(taxFilter.ToLowerInvariant()));

        if (q.FilterValue("isActive") is { } activeRaw &&
            ListQueryHelpers.TryParseBool(activeRaw, out var isActive))
            query = query.Where(p => p.IsActive == isActive);
    }

    private static IQueryable<Ingredient> ApplyIngredientSort(IQueryable<Ingredient> query, ListQuery q)
    {
        var desc = q.IsDescending;
        return (q.SortBy?.ToLowerInvariant() ?? "name") switch
        {
            "ingredientcategoryname" => desc
                ? query.OrderByDescending(i => i.IngredientCategory.Name).ThenBy(i => i.Name)
                : query.OrderBy(i => i.IngredientCategory.Name).ThenBy(i => i.Name),
            "unit" => desc ? query.OrderByDescending(i => i.Unit) : query.OrderBy(i => i.Unit),
            "unitcost" => desc ? query.OrderByDescending(i => i.UnitCost) : query.OrderBy(i => i.UnitCost),
            "stockquantity" => desc ? query.OrderByDescending(i => i.StockQuantity) : query.OrderBy(i => i.StockQuantity),
            "reorderlevel" => desc ? query.OrderByDescending(i => i.ReorderLevel) : query.OrderBy(i => i.ReorderLevel),
            "isactive" => desc ? query.OrderByDescending(i => i.IsActive) : query.OrderBy(i => i.IsActive),
            _ => desc ? query.OrderByDescending(i => i.Name) : query.OrderBy(i => i.Name),
        };
    }

    private static IQueryable<IngredientCategory> ApplyIngredientCategorySort(IQueryable<IngredientCategory> query, ListQuery q)
    {
        var desc = q.IsDescending;
        return (q.SortBy?.ToLowerInvariant() ?? "name") switch
        {
            "description" => desc ? query.OrderByDescending(c => c.Description) : query.OrderBy(c => c.Description),
            "sortorder" => desc ? query.OrderByDescending(c => c.SortOrder) : query.OrderBy(c => c.SortOrder),
            "isactive" => desc ? query.OrderByDescending(c => c.IsActive) : query.OrderBy(c => c.IsActive),
            _ => desc ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name),
        };
    }

    private static IQueryable<Product> ApplyProductSort(IQueryable<Product> query, ListQuery q)
    {
        var desc = q.IsDescending;
        return (q.SortBy?.ToLowerInvariant() ?? "name") switch
        {
            "description" => desc ? query.OrderByDescending(p => p.Description) : query.OrderBy(p => p.Description),
            "sku" => desc ? query.OrderByDescending(p => p.Sku) : query.OrderBy(p => p.Sku),
            "unitprice" => desc ? query.OrderByDescending(p => p.UnitPrice) : query.OrderBy(p => p.UnitPrice),
            "producttypename" => desc
                ? query.OrderByDescending(p => p.ProductType.Name).ThenBy(p => p.Name)
                : query.OrderBy(p => p.ProductType.Name).ThenBy(p => p.Name),
            "compositiontype" => desc
                ? query.OrderByDescending(p => p.CompositionType).ThenBy(p => p.Name)
                : query.OrderBy(p => p.CompositionType).ThenBy(p => p.Name),
            "isactive" => desc ? query.OrderByDescending(p => p.IsActive) : query.OrderBy(p => p.IsActive),
            _ => desc ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
        };
    }

    private static IQueryable<ProductType> ApplyProductTypeSort(IQueryable<ProductType> query, ListQuery q)
    {
        var desc = q.IsDescending;
        return (q.SortBy?.ToLowerInvariant() ?? "name") switch
        {
            "description" => desc ? query.OrderByDescending(t => t.Description) : query.OrderBy(t => t.Description),
            "sortorder" => desc ? query.OrderByDescending(t => t.SortOrder) : query.OrderBy(t => t.SortOrder),
            "isactive" => desc ? query.OrderByDescending(t => t.IsActive) : query.OrderBy(t => t.IsActive),
            _ => desc ? query.OrderByDescending(t => t.Name) : query.OrderBy(t => t.Name),
        };
    }

    private static IQueryable<Provider> ApplyProviderSort(IQueryable<Provider> query, ListQuery q)
    {
        var desc = q.IsDescending;
        return (q.SortBy?.ToLowerInvariant() ?? "name") switch
        {
            "email" => desc ? query.OrderByDescending(p => p.Email) : query.OrderBy(p => p.Email),
            "phone" => desc ? query.OrderByDescending(p => p.Phone) : query.OrderBy(p => p.Phone),
            "taxid" => desc ? query.OrderByDescending(p => p.TaxId) : query.OrderBy(p => p.TaxId),
            "isactive" => desc ? query.OrderByDescending(p => p.IsActive) : query.OrderBy(p => p.IsActive),
            _ => desc ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
        };
    }

    private static IQueryable<Employee> ApplyEmployeeSort(IQueryable<Employee> query, ListQuery q)
    {
        var desc = q.IsDescending;
        return (q.SortBy?.ToLowerInvariant() ?? "name") switch
        {
            "fullname" => desc ? query.OrderByDescending(e => e.FullName) : query.OrderBy(e => e.FullName),
            "jobtitle" => desc ? query.OrderByDescending(e => e.JobTitle) : query.OrderBy(e => e.JobTitle),
            "isactive" => desc ? query.OrderByDescending(e => e.IsActive) : query.OrderBy(e => e.IsActive),
            _ => desc ? query.OrderByDescending(e => e.FullName) : query.OrderBy(e => e.FullName),
        };
    }

    private static IQueryable<DiningTable> ApplyDiningTableSort(IQueryable<DiningTable> query, ListQuery q)
    {
        var desc = q.IsDescending;
        return (q.SortBy?.ToLowerInvariant() ?? "name") switch
        {
            "code" => desc ? query.OrderByDescending(t => t.Code) : query.OrderBy(t => t.Code),
            "zone" => desc ? query.OrderByDescending(t => t.Zone) : query.OrderBy(t => t.Zone),
            "capacity" => desc ? query.OrderByDescending(t => t.Capacity) : query.OrderBy(t => t.Capacity),
            "status" => desc ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
            "isactive" => desc ? query.OrderByDescending(t => t.IsActive) : query.OrderBy(t => t.IsActive),
            _ => desc ? query.OrderByDescending(t => t.Code) : query.OrderBy(t => t.Code),
        };
    }

    private static IQueryable<Customer> ApplyCustomerSort(IQueryable<Customer> query, ListQuery q)
    {
        var desc = q.IsDescending;
        return (q.SortBy?.ToLowerInvariant() ?? "name") switch
        {
            "email" => desc ? query.OrderByDescending(c => c.Email) : query.OrderBy(c => c.Email),
            "phone" => desc ? query.OrderByDescending(c => c.Phone) : query.OrderBy(c => c.Phone),
            "taxid" => desc ? query.OrderByDescending(c => c.TaxId) : query.OrderBy(c => c.TaxId),
            "isactive" => desc ? query.OrderByDescending(c => c.IsActive) : query.OrderBy(c => c.IsActive),
            _ => desc ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name),
        };
    }

    private static IQueryable<Reservation> ApplyReservationSort(IQueryable<Reservation> query, ListQuery q)
    {
        var desc = q.IsDescending;
        return (q.SortBy?.ToLowerInvariant() ?? "name") switch
        {
            "contactname" => desc ? query.OrderByDescending(r => r.ContactName) : query.OrderBy(r => r.ContactName),
            "contactemail" => desc ? query.OrderByDescending(r => r.ContactEmail) : query.OrderBy(r => r.ContactEmail),
            "contactphone" => desc ? query.OrderByDescending(r => r.ContactPhone) : query.OrderBy(r => r.ContactPhone),
            "partysize" => desc ? query.OrderByDescending(r => r.PartySize) : query.OrderBy(r => r.PartySize),
            "startatutc" => desc ? query.OrderByDescending(r => r.StartAtUtc) : query.OrderBy(r => r.StartAtUtc),
            "status" => desc ? query.OrderByDescending(r => r.Status) : query.OrderBy(r => r.Status),
            _ => desc ? query.OrderByDescending(r => r.ContactName) : query.OrderBy(r => r.ContactName),
        };
    }
}
