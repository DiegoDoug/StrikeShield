import { EmptyState } from '@/components/ui/misc'
import { Button } from '@/components/ui/Button'
import { useNavigate } from 'react-router-dom'

export function NotFoundPage() {
  const navigate = useNavigate()
  return (
    <EmptyState
      title="Page not found"
      description="That page doesn't exist or you don't have access to it."
      action={
        <Button variant="secondary" onClick={() => navigate('/clients')}>
          Back to Clients
        </Button>
      }
    />
  )
}
