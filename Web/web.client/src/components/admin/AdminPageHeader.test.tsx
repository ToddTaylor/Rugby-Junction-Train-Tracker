import { describe, expect, it } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import AdminPageHeader from './AdminPageHeader';

describe('AdminPageHeader', () => {
  it('renders title and description with shared admin skin classes', () => {
    const html = renderToStaticMarkup(
      <AdminPageHeader
        title="Users"
        description="Manage user identity, active status, and role assignment."
      />
    );

    expect(html).toContain('class="admin-page-header"');
    expect(html).toContain('class="admin-page-title"');
    expect(html).toContain('class="admin-page-description"');
    expect(html).toContain('Manage user identity, active status, and role assignment.');
  });

  it('renders optional actions container when actions are provided', () => {
    const html = renderToStaticMarkup(
      <AdminPageHeader
        title="Railroads"
        description="Manage railroads."
        actions={<button type="button">Add Railroad</button>}
      />
    );

    expect(html).toContain('class="admin-page-actions"');
    expect(html).toContain('Add Railroad');
  });
});
