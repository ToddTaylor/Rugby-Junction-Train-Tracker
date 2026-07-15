import { createElement } from 'react';

const adminSortIconStyle = {
  fontSize: '1.1em',
  lineHeight: 1,
};

const AdminSortAscendingIcon = () => createElement('span', { style: adminSortIconStyle }, '⬆');
const AdminSortDescendingIcon = () => createElement('span', { style: adminSortIconStyle }, '⬇');
const AdminSortUnsortedIcon = () => createElement('span', { style: adminSortIconStyle }, '⇅');

export const adminDataGridSlots = {
  columnSortedAscendingIcon: AdminSortAscendingIcon,
  columnSortedDescendingIcon: AdminSortDescendingIcon,
  columnUnsortedIcon: AdminSortUnsortedIcon,
};

// DataGridStyles.ts
// Shared MUI DataGrid sx styling for consistent admin pages

export const adminDataGridSx = {
  width: '100%',
  '--DataGrid-headerHeight': '51px',
  backgroundColor: '#2a2a2a',
  color: '#e0e0e0',
  border: '1px solid #444',
  borderRadius: 1,
  '& .MuiDataGrid-main': {
    backgroundColor: '#2a2a2a',
  },
  '& .MuiDataGrid-virtualScroller': {
    backgroundColor: '#2a2a2a',
  },
  '& .MuiDataGrid-filler': {
    backgroundColor: '#1a1a1a !important',
    borderColor: '#444 !important',
  },
  '& .MuiDataGrid-scrollbarFiller': {
    backgroundColor: '#1a1a1a !important',
  },
  '& .MuiDataGrid-columnHeadersWrapper': {
    backgroundColor: '#1a1a1a !important',
    borderColor: '#444 !important',
  },
  '& .MuiDataGrid-columnHeadersInner': {
    backgroundColor: '#1a1a1a !important',
  },
  '& .MuiDataGrid-columnHeaders': {
    backgroundColor: '#1a1a1a',
    borderBottom: '2px solid #444',
    minHeight: '51px !important',
    maxHeight: '51px !important',
  },
  '& .MuiDataGrid-columnHeader': {
    border: 'none',
    borderBottom: 'none !important',
    backgroundColor: '#1a1a1a',
    color: 'inherit',
    minHeight: '51px !important',
    maxHeight: '51px !important',
    lineHeight: '51px',
    padding: '0 15px',
  },
  '& .MuiDataGrid-columnHeader--withRightBorder': {
    borderRight: 'none !important',
  },
  '& .MuiDataGrid-columnSeparator': {
    display: 'none',
  },
  '& .MuiDataGrid-columnHeaderTitle': {
    color: '#e0e0e0',
    fontWeight: 600,
    fontSize: '1rem',
  },
  '& .MuiDataGrid-menuIcon': {
    display: 'none',
  },
  '& .MuiDataGrid-iconButtonContainer': {
    visibility: 'visible',
    width: 'auto',
    marginLeft: '4px',
  },
  '& .MuiDataGrid-sortButton': {
    color: '#e0e0e0',
    padding: 0,
  },
  '& .MuiDataGrid-sortIcon': {
    opacity: 1,
  },
  '& .MuiDataGrid-columnSeparator[data-field="actions"]': {
    display: 'none',
  },
  '& .MuiDataGrid-cell': {
    color: '#ccc',
    borderTop: 'none',
    borderBottom: '1px solid #333',
    borderColor: '#333',
    padding: '0 15px',
  },
  '& .MuiDataGrid-row': {
    backgroundColor: '#2a2a2a',
  },
  '& .MuiDataGrid-row:hover': {
    backgroundColor: '#303030',
  },
  '& .MuiDataGrid-row.Mui-selected': {
    backgroundColor: 'rgba(44, 137, 232, 0.2) !important',
    '&:hover': {
      backgroundColor: 'rgba(44, 137, 232, 0.35) !important',
    },
  },
  '& .MuiTablePagination-root': {
    color: '#e0e0e0',
  },
  '& .MuiDataGrid-footerContainer': {
    backgroundColor: '#1a1a1a',
    borderTop: '1px solid #444',
  },
  '& .MuiTablePagination-toolbar': {
    backgroundColor: '#1a1a1a',
    minHeight: '52px',
  },
  '& .MuiTablePagination-selectLabel, & .MuiTablePagination-displayedRows': {
    color: '#e0e0e0',
    fontSize: '0.9rem',
  },
  '& .MuiTablePagination-input': {
    backgroundColor: '#243042',
    color: '#f2f6fc',
    border: '1px solid #6f86a6',
    borderRadius: '4px',
    minWidth: '48px',
  },
  '& .MuiTablePagination-input .MuiSelect-select': {
    backgroundColor: 'transparent',
    border: 'none',
    fontWeight: 600,
    paddingLeft: '8px',
    paddingRight: '24px',
  },
  '& .MuiTablePagination-selectIcon': {
    color: '#dce8f7',
  },
  '& .MuiIconButton-root': {
    color: '#e0e0e0',
  },
  '& .MuiIconButton-root.Mui-disabled': {
    color: '#555 !important',
    opacity: 0.5,
  },
};

export default adminDataGridSx;
