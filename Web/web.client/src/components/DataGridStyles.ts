// DataGridStyles.ts
// Shared MUI DataGrid sx styling for consistent admin pages

export const adminDataGridSx = {
  maxHeight: 750,
  minHeight: 550,
  width: '100%',
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
    backgroundColor: '#333 !important',
    borderColor: '#444 !important',
  },
  '& .MuiDataGrid-scrollbarFiller': {
    backgroundColor: '#333 !important',
  },
  '& .MuiDataGrid-columnHeadersWrapper': {
    backgroundColor: '#333 !important',
    borderColor: '#444 !important',
  },
  '& .MuiDataGrid-columnHeadersInner': {
    backgroundColor: '#333 !important',
  },
  '& .MuiDataGrid-columnHeader': {
    border: 'none', // remove all borders
    backgroundColor: '#333', // match header row exactly
    color: 'inherit',
    borderColor: 'rgb(68, 68, 68) !important',
  },
  '& .MuiDataGrid-columnSeparator': {
    position: 'absolute',
    overflow: 'hidden',
    zIndex: 30,
    display: 'flex',
    flexDirection: 'column',
    justifyContent: 'center',
    alignItems: 'center',
    maxWidth: '10px',
    color: 'var(--DataGrid-t-color-border-base)',
  },
  '& .MuiDataGrid-columnHeader:not(:last-child)': {
    borderRight: '1px solid #444',
  },
  '& .MuiDataGrid-columnHeader:last-child': {
    borderRight: 'none',
    borderBottom: 'none',
  },
  '& .MuiDataGrid-columnHeaderTitle': {
    color: '#e0e0e0',
    fontWeight: 600,
  },
  '& .MuiDataGrid-columnSeparator[data-field="actions"]': {
    display: 'none',
  },
  '& .MuiDataGrid-cell': {
    color: '#e0e0e0',
    borderColor: '#444',
  },
  '& .MuiDataGrid-row:hover': {
    backgroundColor: '#3a3a3a',
  },
  '& .MuiDataGrid-row.Mui-selected': {
    backgroundColor: '#1e3a5f !important',
    '&:hover': {
      backgroundColor: '#0d47a1 !important',
    },
  },
  '& .MuiTablePagination-root': {
    color: '#e0e0e0',
  },
  '& .MuiTablePagination-toolbar': {
    backgroundColor: '#333',
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
