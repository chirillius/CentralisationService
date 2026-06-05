import { Alert, Snackbar } from '@mui/material';
import { useAppStore } from '../store';

const GlobalFeedback = () => {
  const { alertState, closeAlert, snackbarState, closeSnackbar } = useAppStore();

  return (
    <>
      <Snackbar
        open={alertState.open}
        autoHideDuration={5000}
        onClose={closeAlert}
        anchorOrigin={{ vertical: 'top', horizontal: 'center' }}
        sx={{ top: '50% !important', transform: 'translateY(-50%)' }}
      >
        <Alert onClose={closeAlert} severity="error" variant="filled" sx={{ width: '100%', maxWidth: 560, boxShadow: '0 24px 72px rgba(0,0,0,0.45)' }}>
          {alertState.title ? `${alertState.title}: ${alertState.message}` : alertState.message}
        </Alert>
      </Snackbar>
      <Snackbar
        open={snackbarState.open}
        autoHideDuration={2600}
        onClose={closeSnackbar}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert onClose={closeSnackbar} severity="success" variant="filled" sx={{ width: '100%' }}>
          {snackbarState.message}
        </Alert>
      </Snackbar>
    </>
  );
};

export default GlobalFeedback;
