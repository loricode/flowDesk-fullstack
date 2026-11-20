import { alpha, createTheme } from '@mui/material/styles';

/**
 * Paleta FlowDesk (compartida con la app WPF: desktop/FlowDesk.Desktop/Themes/FlowDeskTheme.xaml)
 *  - Sapote ......... primario: acciones, foco, progreso
 *  - Gris agradable . fondo de la aplicación, bordes y texto secundario
 *  - Negro azulado .. barra lateral, encabezados y texto principal
 */
export const palette = {
  sapote: { main: '#D9734E', light: '#F6D3C4', dark: '#B0532F', contrastText: '#FFFFFF' },
  ink: { main: '#1B2533', soft: '#273345', muted: '#8D9AAD' },
  gray: { background: '#EEF0F3', border: '#E1E5EA', text: '#6B7480' },
  success: '#3E8E6A',
  danger: '#C2453B',
  warning: '#C98A2B',
  info: '#5B7088',
} as const;

export const flowDeskTheme = createTheme({
  palette: {
    mode: 'light',
    primary: palette.sapote,
    secondary: { main: palette.ink.main, light: palette.ink.soft, contrastText: '#FFFFFF' },
    background: { default: palette.gray.background, paper: '#FFFFFF' },
    text: { primary: palette.ink.main, secondary: palette.gray.text },
    divider: palette.gray.border,
    success: { main: palette.success },
    error: { main: palette.danger },
    warning: { main: palette.warning },
    info: { main: palette.info },
  },
  shape: { borderRadius: 12 },
  typography: {
    fontFamily: '"Inter Variable", "Inter", "Roboto", "Segoe UI", system-ui, sans-serif',
    h4: { fontWeight: 700, letterSpacing: '-0.02em' },
    h5: { fontWeight: 700, letterSpacing: '-0.01em' },
    h6: { fontWeight: 650 },
    subtitle1: { fontWeight: 600 },
    button: { textTransform: 'none', fontWeight: 600 },
    overline: { fontWeight: 700, letterSpacing: '0.08em' },
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: { body: { backgroundColor: palette.gray.background } },
    },
    MuiPaper: {
      defaultProps: { elevation: 0 },
      styleOverrides: {
        root: { backgroundImage: 'none' },
        outlined: { borderColor: palette.gray.border },
      },
    },
    MuiCard: {
      defaultProps: { variant: 'outlined' },
      styleOverrides: { root: { borderRadius: 16, borderColor: palette.gray.border } },
    },
    MuiButton: {
      defaultProps: { disableElevation: true },
      styleOverrides: { root: { borderRadius: 10, paddingInline: 18 } },
    },
    MuiChip: {
      styleOverrides: { root: { fontWeight: 600, borderRadius: 8 } },
    },
    MuiTableHead: {
      styleOverrides: {
        root: {
          '& .MuiTableCell-head': {
            color: palette.gray.text,
            fontWeight: 600,
            fontSize: 12,
            textTransform: 'uppercase',
            letterSpacing: '0.04em',
            backgroundColor: '#F7F8FA',
          },
        },
      },
    },
    MuiTableRow: {
      styleOverrides: {
        root: { '&.MuiTableRow-hover:hover': { backgroundColor: alpha(palette.sapote.main, 0.05) } },
      },
    },
    MuiOutlinedInput: {
      styleOverrides: { root: { borderRadius: 10, backgroundColor: '#FFFFFF' } },
    },
    MuiDialog: {
      styleOverrides: { paper: { borderRadius: 18 } },
    },
    MuiTab: {
      styleOverrides: { root: { textTransform: 'none', fontWeight: 600, minHeight: 44 } },
    },
  },
});
