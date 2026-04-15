# Admin Products Plan (Phase 1)

## Scope (Implemented in this phase)

- Real admin products list page: `/admin/products`
- Visible **Add Product** button (placeholder destination): `/admin/products/new`
- Dynamic product admin/details route: `/admin/products/:productId`
- Clickable product items from list to details page
- Real single-product admin details page
- Basic search using `q` on **Title** and **Slug**
- Basic sort options only:
  - `newest`
  - `alphabetical`
  - `active_first`
  - `inactive_first`

## Out of Scope (Explicitly not in Phase 1)

- Best-selling / most-profitable sorting
- Real analytics metrics (orders/profit)
- Full create/edit product forms
- Schema migrations (unless a blocker appears)

## Backend plan

### Endpoints

1. `GET /api/v1/admin/products`
   - query params:
     - `q` (search over `Title`, `Slug`)
     - `sort` (`newest`, `alphabetical`, `active_first`, `inactive_first`)
   - store-aware via existing admin policy + store claim.

2. `GET /api/v1/admin/products/{id}`
   - returns single store-scoped product with:
     - title, slug, status, image, description
     - categories
     - variants summary/details

## Frontend plan

### Routes

- `/admin/products` -> list page (real data)
- `/admin/products/new` -> placeholder page (for now)
- `/admin/products/:productId` -> details page (real data)

### List page contents

- search input (`q`)
- sort dropdown (4 phase-1 options)
- add button
- clickable product rows/cards

### Details page contents

- title
- slug
- status
- categories
- image
- variants summary/details
- stats section as **placeholder only**

## Testing focus

- admin list unauthorized behavior remains correct
- list supports search and phase-1 sort modes
- single product details endpoint returns expected store-scoped details
